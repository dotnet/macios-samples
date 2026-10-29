// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import Foundation
import StoreKit
import IOKit

@objc(StoreBridge)
@MainActor
public final class StoreBridge: NSObject {
    private var updates: Task<Void, Never>?
    private var pending: [UInt64: StoreKit.Transaction] = [:]
    private var readyToFinish: [UInt64: StoreKit.Transaction] = [:]
    private var productID = ""
    private var stopped = false

    @objc(startWithProductID:changed:)
    public func start(productID: String, changed: @escaping (NSError?) -> Void) {
        stop()
        stopped = false
        self.productID = productID
        updates = Task { [weak self] in
            for await result in StoreKit.Transaction.updates {
                guard let self, !Task.isCancelled, !self.stopped else { return }
                do {
                    let transaction = try self.verified(result)
                    guard transaction.productID == self.productID else { continue }
                    self.pending[transaction.id] = transaction
                    changed(nil)
                } catch {
                    changed(error as NSError)
                }
            }
        }
    }

    @objc(loadProduct:completion:)
    public func loadProduct(_ identifier: String, completion: @escaping (String?, NSError?) -> Void) {
        Task {
            do {
                guard let product = try await Product.products(for: [identifier]).first,
                      product.type == .nonConsumable else {
                    throw failure("The configured non-consumable product was not returned by the store.")
                }
                completion("\(product.displayName) - \(product.displayPrice)", nil)
            } catch {
                completion(nil, error as NSError)
            }
        }
    }

    @objc(purchase:completion:)
    public func purchase(_ identifier: String, completion: @escaping (String?, NSError?) -> Void) {
        Task {
            do {
                guard let product = try await Product.products(for: [identifier]).first,
                      product.type == .nonConsumable else {
                    throw failure("The configured non-consumable product was not returned by the store.")
                }
                switch try await product.purchase() {
                case .success(let result):
                    let transaction = try verified(result)
                    pending[transaction.id] = transaction
                    completion("purchased", nil)
                case .pending:
                    completion("pending", nil)
                case .userCancelled:
                    completion("cancelled", nil)
                @unknown default:
                    throw failure("StoreKit returned an unsupported purchase result.")
                }
            } catch {
                completion(nil, error as NSError)
            }
        }
    }

    @objc(readEntitlementWithCompletion:)
    public func readEntitlement(completion: @escaping (Bool, NSError?) -> Void) {
        Task {
            do {
                for await result in StoreKit.Transaction.unfinished {
                    let transaction = try verified(result)
                    if transaction.productID == productID {
                        pending[transaction.id] = transaction
                    }
                }
                var entitled = false
                for await result in StoreKit.Transaction.currentEntitlements {
                    let transaction = try verified(result)
                    if transaction.productID == productID,
                       transaction.productType == .nonConsumable,
                       transaction.revocationDate == nil {
                        entitled = true
                    }
                }
                readyToFinish = pending.filter {
                    $0.value.productType == .nonConsumable &&
                    (entitled || $0.value.revocationDate != nil)
                }
                completion(entitled, nil)
            } catch {
                completion(false, error as NSError)
            }
        }
    }

    @objc(restoreWithCompletion:)
    public func restore(completion: @escaping (NSError?) -> Void) {
        Task {
            do {
                try await AppStore.sync()
                completion(nil)
            } catch {
                completion(error as NSError)
            }
        }
    }

    // Called only after the app has successfully applied the verified entitlement.
    @objc(finishWithCompletion:)
    public func finish(completion: @escaping (NSError?) -> Void) {
        Task {
            let transactions = readyToFinish
            readyToFinish.removeAll()
            for (id, transaction) in transactions {
                await transaction.finish()
                pending.removeValue(forKey: id)
            }
            completion(nil)
        }
    }

    @objc public func stop() {
        stopped = true
        updates?.cancel()
        updates = nil
        pending.removeAll()
        readyToFinish.removeAll()
    }

    private func verified<T>(_ result: VerificationResult<T>) throws -> T {
        switch result {
        case .verified(let value): return value
        case .unverified(_, let error): throw error
        }
    }

    private func failure(_ message: String) -> NSError {
        NSError(domain: "StoreBridge", code: 1, userInfo: [NSLocalizedDescriptionKey: message])
    }

    @objc public static func deviceIdentifier() -> NSData? {
        for (name, builtIn) in [("en0", true), ("en1", true), ("en0", false)] {
            var iterator: io_iterator_t = 0
            guard let matching = IOBSDNameMatching(kIOMainPortDefault, 0, name),
                  IOServiceGetMatchingServices(kIOMainPortDefault, matching, &iterator) == KERN_SUCCESS else {
                continue
            }
            defer { IOObjectRelease(iterator) }
            var service = IOIteratorNext(iterator)
            while service != 0 {
                let candidate = service
                defer { IOObjectRelease(candidate) }
                if let property = IORegistryEntryCreateCFProperty(candidate, "IOBuiltin" as CFString, kCFAllocatorDefault, 0),
                   let value = property.takeRetainedValue() as? Bool, value == builtIn {
                    if let address = IORegistryEntrySearchCFProperty(
                        candidate, kIOServicePlane, "IOMACAddress" as CFString, kCFAllocatorDefault,
                        IOOptionBits(kIORegistryIterateRecursively | kIORegistryIterateParents)) as? Data {
                        return address as NSData
                    }
                }
                service = IOIteratorNext(iterator)
            }
        }
        return nil
    }
}
