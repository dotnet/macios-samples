Exception marshaling
====================

This is a sample solution with a project for each platform to show how exception marshaling works.

This sample assumes knowledge of how exception marshaling works, which is documented here: [Exception Marshaling][1]

## Example #1 - default behavior

By default all exceptions are marshaled in both iOS and macOS projects (and tvOS and Mac Catalyst projects as well, although this sample doesn't support those platforms).

### iOS simulator (Debug)

1. Run the iOS project in the simulator using the Debug configuration.
2. Tap `Throw Objective-C exception`.
3. Tap `Throw`.

You'll see the following Application Output:

```
Marshalling Objective-C exception
    Exception: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
    Mode: ThrowManagedException
    Target mode:
Caught managed exception: ObjCRuntime.ObjCException: Objective-C exception thrown.  Name: NSInvalidArgumentException Reason: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
Native stack trace:
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
   at Foundation.NSMutableDictionary.LowlevelSetObject(IntPtr obj, IntPtr key) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/Foundation/NSMutableDictionary.cs:line 338
   at ExceptionMarshaling.Exceptions.ThrowObjectiveCException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 48
Native stack trace:
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
```

What happens here?

1. Since interception of Objective-C exceptions is enabled by default,
   the first thing that happens is that the Objective-C exception
   is intercepted, the `MarshalObjectiveCException` event is raised and
   the test project's event handler is called:

    ```
    Marshalling Objective-C exception
        Exception: *** setObjectForKey: key cannot be nil
        Mode: UnwindManagedCode
        Target mode: 
    ```

2. The default target mode is not changed, which means that the iOS SDK's
   runtime will catch the Objective-C exception, and convert it into a managed
   exception, and we end up in the test project's managed exception handler:

   ```
   Caught managed exception: ObjCRuntime.ObjCException: Objective-C exception thrown.  Name: NSInvalidArgumentException Reason: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
    Native stack trace:
        0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
        1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
        2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
        3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
        4   ???                                 0x000000019c58346a 0x0 + 6917993578
        at Foundation.NSMutableDictionary.LowlevelSetObject(IntPtr obj, IntPtr key) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/Foundation/NSMutableDictionary.cs:line 338
        at ExceptionMarshaling.Exceptions.ThrowObjectiveCException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 48
    Native stack trace:
        0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
        1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
        2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
        3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
        4   ???                                 0x000000019c58346a 0x0 + 6917993578
   ```

### iOS Device (Debug)

1. Run the iOS project on a device using the Debug configuration.
2. Tap `Throw Objective-C exception`.
3. Tap `Throw`.

Since Objective-C exceptions are always intercepted by default, this is identical to the simulator case from just above.

```
Marshalling Objective-C exception
    Exception: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
    Mode: ThrowManagedException
    Target mode:
Microsoft.iOS: Processing Objective-C exception for exception marshalling (mode: 2):
*** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
(
    0   CoreFoundation                      0x00000001b0a7d29c DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 627356
    1   libobjc.A.dylib                     0x00000001c97b1744 objc_exception_throw + 60
    2   CoreFoundation                      0x00000001b0b88318 DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 1721112
    3   CoreFoundation                      0x00000001b0b935a0 DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 1766816
    4   CoreFoundation                      0x00000001b0a325ec DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 321004
    5   ExceptionMarshaling                 0x00000001057bd944 xamarin_dyn_objc_msgSend + 160
    6   ExceptionMarshaling                 0x000000010579cbb8 wrapper_managed_to_native_ObjCRuntime_Messaging_void_objc_msgSend_IntPtr_IntPtr_intptr_intptr_intptr_intptr + 168
    7   ExceptionMarshaling                 0x0000000105773bf8 Foundation_NSMutableDictionary_LowlevelSetObject_intptr_intptr + 88
       [...]
)
Caught managed exception: ObjCRuntime.ObjCException: Objective-C exception thrown.  Name: NSInvalidArgumentException Reason: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
Native stack trace:
    0   CoreFoundation                      0x00000001b0a7d29c DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 627356
    1   libobjc.A.dylib                     0x00000001c97b1744 objc_exception_throw + 60
    2   CoreFoundation                      0x00000001b0b88318 DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 1721112
    3   CoreFoundation                      0x00000001b0b935a0 DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 1766816
    4   CoreFoundation                      0x00000001b0a325ec DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 321004
    5   ExceptionMarshaling                 0x00000001057bd944 xamarin_dyn_objc_msgSend + 160
    6   ExceptionMarshaling                 0x000000010579cbb8 wrapper_managed_to_native_ObjCRuntime_Messaging_void_objc_msgSend_IntPtr_IntPtr_intptr_intptr_intptr_intptr + 168
    7   ExceptionMarshaling                 0x0000000105773bf8 Foundation_NSMutableDictionary_LowlevelSetObject_intptr_intptr + 88
       [...]
    41  ExceptionMarshaling                 0x00000001058db1f0 mono_runtime_exec_main_checked + 116
    42  ExceptionMarshaling                 0x0000000105932c88 mono_jit_exec + 356
    43  ExceptionMarshaling                 0x00000001057bc8b4 xamarin_main + 2312
    44  ExceptionMarshaling                 0x00000001059a6910 main + 64
    45  dyld                                0x0000000106331da4 start + 520
   at Foundation.NSMutableDictionary.LowlevelSetObject(IntPtr obj, IntPtr key) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/Foundation/NSMutableDictionary.cs:line 338
   at ExceptionMarshaling.Exceptions.ThrowObjectiveCException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 48
Native stack trace:
    0   CoreFoundation                      0x00000001b0a7d29c DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 627356
    1   libobjc.A.dylib                     0x00000001c97b1744 objc_exception_throw + 60
    2   CoreFoundation                      0x00000001b0b88318 DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 1721112
    3   CoreFoundation                      0x00000001b0b935a0 DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 1766816
    4   CoreFoundation                      0x00000001b0a325ec DA3C2E10-0C3D-3FBC-9C3E-E950EBA7020F + 321004
    5   ExceptionMarshaling                 0x00000001057bd944 xamarin_dyn_objc_msgSend + 160
    6   ExceptionMarshaling                 0x000000010579cbb8 wrapper_managed_to_native_ObjCRuntime_Messaging_void_objc_msgSend_IntPtr_IntPtr_intptr_intptr_intptr_intptr + 168
    7   ExceptionMarshaling                 0x0000000105773bf8 Foundation_NSMutableDictionary_LowlevelSetObject_intptr_intptr + 88
       [...]
    41  ExceptionMarshaling                 0x00000001058db1f0 mono_runtime_exec_main_checked + 116
    42  ExceptionMarshaling                 0x0000000105932c88 mono_jit_exec + 356
    43  ExceptionMarshaling                 0x00000001057bc8b4 xamarin_main + 2312
    44  ExceptionMarshaling                 0x00000001059a6910 main + 64
    45  dyld                                0x0000000106331da4 start + 520
```

### macOS (Debug)

1. Run the macOS project using the Debug configuration.
2. Click on `Throw Objective-C exception`.

The following shows up in the Application Output:

```
Marshalling Objective-C exception
    Exception: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
    Mode: ThrowManagedException
    Target mode:
Caught managed exception: ObjCRuntime.ObjCException: Objective-C exception thrown.  Name: NSInvalidArgumentException Reason: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
Native stack trace:
    0   CoreFoundation                      0x000000019c9562cc __exceptionPreprocess + 176
    1   libobjc.A.dylib                     0x000000019c43a158 objc_exception_throw + 60
    2   CoreFoundation                      0x000000019c89b4c8 -[__NSDictionaryM setObject:forKey:] + 1068
    3   ExceptionMarshaling                 0x0000000102b8f2cc xamarin_dyn_objc_msgSend + 160
        [...]
    49  ExceptionMarshaling                 0x0000000102b8e270 xamarin_main + 884
    50  ExceptionMarshaling                 0x0000000102c2eac8 main + 64
    51  dyld                                0x000000019c477154 start + 2476

   at ObjCRuntime.Messaging.void_objc_msgSend_IntPtr_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2)
   at Foundation.NSMutableDictionary.LowlevelSetObject(IntPtr obj, IntPtr key) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/Foundation/NSMutableDictionary.cs:line 336
   at ExceptionMarshaling.Exceptions.ThrowObjectiveCException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 48
Native stack trace:
    0   CoreFoundation                      0x000000019c9562cc __exceptionPreprocess + 176
    1   libobjc.A.dylib                     0x000000019c43a158 objc_exception_throw + 60
    2   CoreFoundation                      0x000000019c89b4c8 -[__NSDictionaryM setObject:forKey:] + 1068
    3   ExceptionMarshaling                 0x0000000102b8f2cc xamarin_dyn_objc_msgSend + 160
        [...]
    49  ExceptionMarshaling                 0x0000000102b8e270 xamarin_main + 884
    50  ExceptionMarshaling                 0x0000000102c2eac8 main + 64
    51  dyld                                0x000000019c477154 start + 2476
```

Since Objective-C exceptions are always intercepted by default, this is identical to the iOS cases from just above.

## Example #2 - unwinding managed frames for an Objective-C exception

### iOS

1. Run the iOS project in the simulator using the Debug configuration.
2. Tap `Throw Objective-C exception`.
3. Enter `Marshal Objective-C exception mode` and select `Unwind managed code`.
3. Go back and tap `Throw`.

This is the Application Output - things went rather bad this time:

```
Marshalling Objective-C exception
    Exception: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
    Mode: ThrowManagedException
    Target mode: UnwindManagedCode
*** Terminating app due to uncaught exception 'NSInvalidArgumentException', reason: '*** -[__NSDictionaryM setObject:forKey:]: key cannot be nil'
*** First throw call stack:
(
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
)
*** Terminating app due to uncaught exception 'NSInvalidArgumentException', reason: '*** -[__NSDictionaryM setObject:forKey:]: key cannot be nil'
*** First throw call stack:
(
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
)
Microsoft.iOS: Received unhandled ObjectiveC exception: NSInvalidArgumentException *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
Marshalling managed exception
    Exception: ObjCRuntime.ObjCException: Objective-C exception thrown.  Name: NSInvalidArgumentException Reason: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
Native stack trace:
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
    at ObjCRuntime.Runtime.ThrowNSException(IntPtr ns_exception) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/ObjCRuntime/Runtime.cs:line 564
    at ObjCRuntime.Runtime.throw_ns_exception(IntPtr exc) in /Users/builder/azdo/_work/9/s/xamarin-macios/runtime/Delegates.generated.cs:line 279
    at Foundation.NSMutableDictionary.LowlevelSetObject(IntPtr obj, IntPtr key) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/Foundation/NSMutableDictionary.cs:line 338
    at ExceptionMarshaling.Exceptions.ThrowObjectiveCException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 48
Native stack trace:
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
    Mode: Default
    Target mode:
*** Terminating app due to uncaught exception 'NSInvalidArgumentException', reason: '*** -[__NSDictionaryM setObject:forKey:]: key cannot be nil'
*** First throw call stack:
(
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
)
*** Terminating app due to uncaught exception 'NSInvalidArgumentException', reason: '*** -[__NSDictionaryM setObject:forKey:]: key cannot be nil'
*** First throw call stack:
(
    0   CoreFoundation                      0x000000010d58e565 __exceptionPreprocess + 242
    1   libobjc.A.dylib                     0x000000011fd04116 objc_exception_throw + 62
    2   CoreFoundation                      0x000000010d5ff4a2 -[__NSDictionaryM setObject:forKey:] + 1329
    3   libxamarin-dotnet-debug.dylib       0x000000010a3aaef9 xamarin_dyn_objc_msgSend + 217
    4   ???                                 0x000000019c58346a 0x0 + 6917993578
)
Microsoft.iOS: Received unhandled ObjectiveC exception: NSInvalidArgumentException *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
Detected recursion when handling uncaught Objective-C exception: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil

=================================================================
    Native Crash Reporting
=================================================================
Got a SIGABRT while executing native code. This usually indicates
a fatal error in the mono runtime or one of the native libraries
used by your application.
=================================================================

=================================================================
    Native stacktrace:
=================================================================
    0x10a792f2f - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libmonosgen-2.0.dylib : mono_dump_native_crash_info
    0x10a72f8be - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libmonosgen-2.0.dylib : mono_handle_native_crash
    0x10a932b78 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libmonosgen-2.0.dylib : sigabrt_signal_handler.cold.1
    0x10a7928c0 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libmonosgen-2.0.dylib : mono_runtime_setup_stat_profiler
    0x121c34fdd - /usr/lib/system/libsystem_platform.dylib : _sigtramp
    0x0 - Unknown
    0x121b86d25 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/system/libsystem_c.dylib : abort
    0x10a395e93 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : _ZL17exception_handlerP11NSException
    0x10d58e9b2 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation : __handleUncaughtException
    0x11fce26f2 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libobjc.A.dylib : _ZL15_objc_terminatev
    0x11fcaaacb - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libc++abi.dylib : _ZSt11__terminatePFvvE
    0x11fcad717 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libc++abi.dylib : __cxa_get_exception_ptr
    0x11fcad6fc - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libc++abi.dylib : _ZN10__cxxabiv1L12failed_throwEPNS_15__cxa_exceptionE
    0x11fd04219 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libobjc.A.dylib : objc_exception_throw
    0x10a395762 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : xamarin_process_managed_exception
    0x10a3952ab - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : xamarin_process_managed_exception_gchandle
    0x10a395265 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : xamarin_ftnptr_exception_handler
    0x10a72ed47 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libmonosgen-2.0.dylib : mono_handle_exception_internal
    0x10a72d505 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libmonosgen-2.0.dylib : mono_handle_exception
    0x10a78cf89 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libmonosgen-2.0.dylib : mono_amd64_throw_exception
    0x10965a5b0 - Unknown
    0x10a395eab - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : _ZL17exception_handlerP11NSException
    0x10d58e9b2 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation : __handleUncaughtException
    0x11fce26f2 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libobjc.A.dylib : _ZL15_objc_terminatev
    0x11fcaaacb - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libc++abi.dylib : _ZSt11__terminatePFvvE
    0x11fcad717 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libc++abi.dylib : __cxa_get_exception_ptr
    0x11fcad6fc - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libc++abi.dylib : _ZN10__cxxabiv1L12failed_throwEPNS_15__cxa_exceptionE
    0x11fd04219 - /Library/Developer/CoreSimulator/Volumes/iOS_22C150/Library/Developer/CoreSimulator/Profiles/Runtimes/iOS 18.2.simruntime/Contents/Resources/RuntimeRoot/usr/lib/libobjc.A.dylib : objc_exception_throw
    0x10a3976e4 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : xamarin_process_nsexception_using_mode
    0x10a39758b - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : xamarin_process_nsexception
    0x10a3aaf29 - $HOME/Library/Developer/CoreSimulator/Devices/A91E6E62-554A-4441-A2B8-428E6D67AA7F/data/Containers/Bundle/Application/E68610B2-27E0-4229-9A92-1538B1E78539/ExceptionMarshaling.app/libxamarin-dotnet-debug.dylib : xamarin_dyn_objc_msgSend
    0x19c58346a - Unknown

=================================================================
    Basic Fault Address Reporting
=================================================================
Memory around native instruction pointer (0x121cd4d96):0x121cd4d86  ff ff c3 90 90 90 b8 48 01 00 02 49 89 ca 0f 05  .......H...I....
0x121cd4d96  73 08 48 89 c7 e9 20 9a ff ff c3 90 90 90 b8 53  s.H... ........S
0x121cd4da6  00 00 02 49 89 ca 0f 05 73 08 48 89 c7 e9 08 9a  ...I....s.H.....
0x121cd4db6  ff ff c3 90 90 90 b8 83 01 00 02 49 89 ca 0f 05  ...........I....

=================================================================
    Managed Stacktrace:
=================================================================
      at <unknown> <0xffffffff>
      at ObjCRuntime.Messaging:void_objc_msgSend_IntPtr_IntPtr <0x000f9>
      at Foundation.NSMutableDictionary:LowlevelSetObject <0x00082>
      at ExceptionMarshaling.Exceptions:ThrowObjectiveCException <0x00082>
      at ExceptionMarshaling.Exceptions:ThrowObjectiveCException <0x00082>
      at ModeTableViewController:RowSelected <0x00482>
      at <Module>:runtime_invoke_void__this___object_object <0x000c7>
      at <unknown> <0xffffffff>
      at UIKit.UIApplication:xamarin_UIApplicationMain <0x00111>
      at UIKit.UIApplication:UIApplicationMain <0x0008a>
      at UIKit.UIApplication:Main <0x001f2>
      at ExceptionMarshaling.Application:Main <0x0004a>
      at <Module>:runtime_invoke_void_object <0x000b0>
=================================================================
```

What happens here?

1. The Objective-C exception is intercepted, and the
   `MarshalObjectiveCException` event is raised and the event handler in the
   test project called:

    ```
    Marshalling Objective-C exception
        Exception: *** -[__NSDictionaryM setObject:forKey:]: key cannot be nil
        Mode: ThrowManagedException
        Target mode: UnwindManagedCode
    ```

2. Since the target mode is modified in the event handler to let the
   Objective-C runtime unwind managed code, we re-throw the Objective-C
   exception.

3. The Objective-C runtime can't find any Objective-C catch handlers, so it
   invokes the handler for uncaught Objective-C exceptions the .NET for iOS
   SDK installed.

4. The handler detects that we're already handling an Objective-C exception,
   and aborts the process to avoid ending up with an infinite recursion.

## Example #3 - unwinding native frames for a managed exception

### iOS (Debug)

1. Run the iOS project in the simulator using the Debug configuration.
2. Tap `Throw managed exception`.
3. Enter `Marshal managed exception mode` and select `Unwind native code`.
3. Go back and tap `Throw`.

```
Marshalling managed exception
    Exception: System.ApplicationException: A managed exception
   at ExceptionMarshaling.ExceptionalObject.ThrowManagedException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 108
    Mode: Default
    Target mode: UnwindNativeCode
Caught managed exception: System.ApplicationException: A managed exception
   at ExceptionMarshaling.ExceptionalObject.ThrowManagedException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 108
--- End of stack trace from previous location ---
   at Foundation.NSObject.PerformSelector(Selector aSelector) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/build/dotnet/ios/generated-sources/Foundation/NSObject.g.cs:line 367
   at ExceptionMarshaling.Exceptions.ThrowManagedExceptionThroughNativeCode() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 58
```

What happened here?

1. The exception was intercepted, and the corresponding `MarshalManagedException` event was called.
2. The event handler asked the runtime to unwind native code, which MonoVM did, and found a managed frame with a managed catch handler further up the stack (after passing through some native frames).
3. This managed catch handler successfully handled the managed exception. 

### macOS (Debug)

1. Run the macOS project using the Debug configuration.
2. Tap `Throw managed exception`.
3. Enter `Marshal managed exception mode` and select `Unwind native code`.
3. Go back and tap `Throw`.

Since Objective-C exceptions are automatically intercepted in Debug
configurations, the `MarshalObjectiveCException` event will be raised, and
since the target mode is modified in the event handler to marshal the
Objective-C exception to a managed exception, the app doesn't crash anymore:

```
Marshalling managed exception
    Exception: System.ApplicationException: A managed exception
   at ExceptionMarshaling.ExceptionalObject.ThrowManagedException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 108
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
--- End of stack trace from previous location ---
   at ObjCRuntime.Runtime.InvokeMethod(MethodBase method, Object instance, IntPtr native_parameters) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/ObjCRuntime/Runtime.CoreCLR.cs:line 792
   at ObjCRuntime.Runtime.InvokeMethod(MonoObject* methodobj, MonoObject* instanceobj, IntPtr native_parameters) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/ObjCRuntime/Runtime.CoreCLR.cs:line 682
   at ObjCRuntime.Runtime.bridge_runtime_invoke_method(MonoObject* method, MonoObject* instance, IntPtr parameters, IntPtr* exception_gchandle) in /Users/builder/azdo/_work/9/s/xamarin-macios/runtime/Delegates.generated.cs:line 1296
    Mode: Default
    Target mode: UnwindNativeCode
2025-01-03 16:48:53.344 ExceptionMarshaling[74598:7662549] Microsoft.macOS: Aborting due to trying to marshal managed exception:
A managed exception (System.ApplicationException)
   at ExceptionMarshaling.ExceptionalObject.ThrowManagedException() in macios-samples/ExceptionMarshaling/Shared/Exceptions.cs:line 108
   at System.RuntimeMethodHandle.InvokeMethod(Object target, Void** arguments, Signature sig, Boolean isConstructor)
   at System.Reflection.MethodBaseInvoker.InvokeWithNoArgs(Object obj, BindingFlags invokeAttr)
--- End of stack trace from previous location ---
   at ObjCRuntime.Runtime.InvokeMethod(MethodBase method, Object instance, IntPtr native_parameters) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/ObjCRuntime/Runtime.CoreCLR.cs:line 792
   at ObjCRuntime.Runtime.InvokeMethod(MonoObject* methodobj, MonoObject* instanceobj, IntPtr native_parameters) in /Users/builder/azdo/_work/9/s/xamarin-macios/src/ObjCRuntime/Runtime.CoreCLR.cs:line 682
   at ObjCRuntime.Runtime.bridge_runtime_invoke_method(MonoObject* method, MonoObject* instance, IntPtr parameters, IntPtr* exception_gchandle) in /Users/builder/azdo/_work/9/s/xamarin-macios/runtime/Delegates.generated.cs:line 1296
[the process terminated]
```

What happened here?

1. The exception was intercepted, and the corresponding `MarshalManagedException` event was called.
2. The event handler asked the runtime to unwind native code, but CoreCLR (the managed runtime for macOS apps) doesn't support unwinding native code, so it aborted the process.

[1]: https://learn.microsoft.com/dotnet/ios/platform/exception-marshaling
