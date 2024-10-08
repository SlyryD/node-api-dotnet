// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// A strong or weak reference to a JS value.
/// </summary>
/// <remarks>
/// <see cref="JSValue"/> and related JS handle structs are not valid after their
/// <see cref="JSValueScope"/> closes -- typically that is when a callback returns. Use a
/// <see cref="JSReference"/> to maintain a reference to a JS value beyond a single scope.
/// <para/>
/// A <see cref="JSReference"/> should be disposed when no longer needed; this allows the JS value
/// to be collected by the JS GC if it has no other references. The <see cref="JSReference"/> class
/// also has a finalizer so that the reference will be released when the C# object is GC'd. However
/// explicit disposal is still preferable when possible.
/// <para/>
/// A "strong" reference ensures the JS value is available at least until the reference is disposed.
/// A "weak" reference does not prevent the JS value from being released collected by the JS
/// garbage-collector, but allows the value to be optimistically retrieved until then.
/// </remarks>
public class JSReference : IDisposable
{
    private readonly napi_ref _handle;
    private readonly napi_env _env;
    private readonly JSRuntimeContext? _context;

    /// <summary>
    /// Creates a new instance of a <see cref="JSReference"/> that holds a strong or weak
    /// reference to a JS value.
    /// </summary>
    /// <param name="value">The JavaScript value to reference.</param>
    /// <param name="isWeak">True if the reference will be "weak", meaning the reference does not
    /// prevent the value from being released and collected by the JS garbage-collector. The
    /// default is false, meaning the value will remain available at least until the "strong"
    /// reference is disposed.
    /// </param>
    public JSReference(JSValue value, bool isWeak = false)
        : this(value.Runtime.CreateReference(
                  (napi_env)JSValueScope.Current,
                  (napi_value)value,
                  isWeak ? 0u : 1u,
                  out napi_ref handle).ThrowIfFailed(handle),
               isWeak)
    {
    }

    /// <summary>
    /// Creates a new instance of a <see cref="JSReference"/> that holds a strong or weak
    /// reference to a JS value.
    /// </summary>
    /// <param name="handle">The reference handle.</param>
    /// <param name="isWeak">True if the handle is for a weak reference. This must match
    /// the existing state of the handle.</param>
    public JSReference(napi_ref handle, bool isWeak = false)
    {
        JSValueScope currentScope = JSValueScope.Current;

        // Thread access to the env will be checked on reference handle use.
        _env = currentScope.UncheckedEnvironmentHandle;
        _handle = handle;
        _context = currentScope.RuntimeContext;
        IsWeak = isWeak;
    }

    /// <summary>
    /// Gets a value indicating whether the reference is weak.
    /// </summary>
    /// <returns>True if the reference is weak, false if it is strong.
    public bool IsWeak { get; private set; }

    /// <summary>
    /// Gets the value handle, or throws an exception if access from the current thread is invalid.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The reference is disposed.</exception>
    /// <exception cref="JSInvalidThreadAccessException">Access to the reference is not valid on
    /// the current thread.</exception>
    public napi_ref Handle
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfInvalidThreadAccess();
            return _handle;
        }
    }

    public static explicit operator napi_ref(JSReference reference)
    {
        if (reference is null) throw new ArgumentNullException(nameof(reference));
        return reference.Handle;
    }

    /// <summary>
    /// Creates a reference to a JS value, if the value is an object, function, or symbol.
    /// </summary>
    /// <param name="value">The JS value to reference.</param>
    /// <param name="isWeak">True to create a weak reference, false to create a strong
    /// reference.</param>
    /// <param name="result">Returns the created reference, or <c>default(JSValue)</c>, equivalent
    /// to <c>undefined</c>, if the value is not a referencable type.</param>
    /// <returns>True if the reference was created, false if the reference could not be created
    /// because the value is not a supported type for references.</returns>
    public static bool TryCreateReference(
        JSValue value, bool isWeak, [NotNullWhen(true)] out JSReference? result)
    {
        napi_status status = value.Runtime.CreateReference(
                  (napi_env)JSValueScope.Current,
                  (napi_value)value,
                  isWeak ? 0u : 1u,
                  out napi_ref handle);
        if (status == napi_status.napi_ok)
        {
            result = new JSReference(handle, isWeak);
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// Gets the synchronization context that must be used to access the referenced value.
    /// </summary>
    /// <remarks>
    /// Use the <see cref="JSSynchronizationContext.Run(Action)" /> method to wrap code that
    /// accesses the referenced value, if there is a possibility that the current execution
    /// context is not already on the correct thread.
    /// </remarks>
    public JSSynchronizationContext? SynchronizationContext => _context?.SynchronizationContext;

    private napi_env Env
    {
        get
        {
            ThrowIfDisposed();
            ThrowIfInvalidThreadAccess();
            return _env;
        }
    }

    /// <summary>
    /// Changes a strong reference into a weak reference, if it is not already weak.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The reference is disposed.</exception>
    public void MakeWeak()
    {
        if (!IsWeak)
        {
            JSValueScope.CurrentRuntime.UnrefReference(Env, _handle, out _).ThrowIfFailed();
            IsWeak = true;
        }
    }

    /// <summary>
    /// Changes a weak reference into a strong reference, if it is not already strong and
    /// the value is still available.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The reference is disposed.</exception>
    public void MakeStrong()
    {
        if (IsWeak)
        {
            JSValueScope.CurrentRuntime.RefReference(Env, _handle, out _).ThrowIfFailed();
            IsWeak = false;
        }
    }

    /// <summary>
    /// Gets the referenced JS value.
    /// </summary>
    /// <returns>The referenced JS value.</returns>
    /// <exception cref="ObjectDisposedException">The reference is disposed.</exception>
    /// <exception cref="NullReferenceException">The reference is weak and the weakly-referenced
    /// JS value is not available.</exception>
    /// <remarks>
    /// Use this method with strong references when the referenced value is expected to be always
    /// available. For weak references, use <see cref="TryGetValue(out JSValue)" /> instead.
    /// </remarks>
    public JSValue GetValue()
    {
        JSValueScope.CurrentRuntime.GetReferenceValue(Env, _handle, out napi_value result)
            .ThrowIfFailed();

        // napi_get_reference_value() returns a null handle if the weak reference is invalid.
        if (result == default)
        {
            throw new NullReferenceException("The weakly-referenced JS value not available.");
        }

        return result;
    }

    /// <summary>
    /// Attempts to get the referenced JS value.
    /// </summary>
    /// <param name="value">Returns the referenced JS value, or <c>default(JSValue)</c>, equivalent
    /// to <c>undefined</c>, if the reference is weak and the value is not available.</param>
    /// <returns>True if the value was obtained, false if the reference is weak and the value is
    /// not available.</returns>
    /// <exception cref="ObjectDisposedException">The reference is disposed.</exception>
    public bool TryGetValue(out JSValue value)
    {
        JSValueScope.CurrentRuntime.GetReferenceValue(Env, _handle, out napi_value result)
            .ThrowIfFailed();

        // napi_get_reference_value() returns a null handle if the weak reference is invalid.
        if (result == default)
        {
            value = default;
            return false;
        }

        value = new JSValue(result);
        return true;
    }

    /// <summary>
    /// Runs an action with the referenced value, using the <see cref="JSSynchronizationContext" />
    /// associated with the reference to switch to the JS thread (if necessary) while operating
    /// on the value.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The reference is disposed.</exception>
    /// <exception cref="NullReferenceException">The reference is weak and the value is not
    /// available.</exception>
    /// <remarks>
    /// This method may be called from any thread. The <paramref name="action" /> delegate is
    /// invoked on the JS thread.
    /// </remarks>
    public void Run(Action<JSValue> action)
    {
        void GetValueAndRunAction()
        {
            JSValue? value = GetValue();
            if (!value.HasValue)
            {
                throw new NullReferenceException("The JS reference is null.");
            }

            action(value.Value);
        }

        JSSynchronizationContext? synchronizationContext = SynchronizationContext;
        if (synchronizationContext != null)
        {
            synchronizationContext.Run(GetValueAndRunAction);
        }
        else
        {
            GetValueAndRunAction();
        }
    }

    /// <summary>
    /// Runs an action with the referenced value, using the <see cref="JSSynchronizationContext" />
    /// associated with the reference to switch to the JS thread (if necessary) while operating
    /// on the value.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The reference is disposed.</exception>
    /// <exception cref="NullReferenceException">The reference is weak and the value is not
    /// available.</exception>
    /// <remarks>
    /// This method may be called from any thread. The <paramref name="action" /> delegate is
    /// invoked on the JS thread.
    /// </remarks>
    public T Run<T>(Func<JSValue, T> action)
    {
        T GetValueAndRunAction()
        {
            JSValue? value = GetValue();
            if (!value.HasValue)
            {
                throw new NullReferenceException("The JS reference is null.");
            }

            return action(value.Value);
        }

        JSSynchronizationContext? synchronizationContext = SynchronizationContext;
        if (synchronizationContext != null)
        {
            return synchronizationContext.Run(GetValueAndRunAction);
        }
        else
        {
            return GetValueAndRunAction();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the reference has been disposed.
    /// </summary>
    /// <remarks>
    /// Note that a weakly-referenced JS value may have been released without the JS reference
    /// itself being disposed. Call <see cref="GetValue()"/> to check if the referenced value is
    /// still available.
    /// </remarks>
    public bool IsDisposed { get; private set; }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(JSReference));
        }
    }

    /// <summary>
    /// Checks that the current thread is the thread that is running the JavaScript environment
    /// that this reference was created in.
    /// </summary>
    /// <exception cref="JSInvalidThreadAccessException">The reference cannot be accessed from the
    /// current thread.</exception>
    private void ThrowIfInvalidThreadAccess()
    {
        JSValueScope currentScope = JSValueScope.Current;
        if ((napi_env)currentScope != _env)
        {
            int threadId = Environment.CurrentManagedThreadId;
            string? threadName = Thread.CurrentThread.Name;
            string threadDescription = string.IsNullOrEmpty(threadName) ?
                $"#{threadId}" : $"#{threadId} \"{threadName}\"";
            string message = "The JS reference cannot be accessed from the current thread.\n" +
                $"Current thread: {threadDescription}. " +
                $"Consider using the synchronization context to switch to the JS thread.";
            throw new JSInvalidThreadAccessException(currentScope, message);
        }
    }

    /// <summary>
    /// Releases the reference.
    /// </summary>
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            IsDisposed = true;

            // The context may be null if the reference was created from a "no-context" scope such
            // as the native host. In that case the reference must be disposed from the JS thread.
            if (_context == null)
            {
                ThrowIfInvalidThreadAccess();
                JSValueScope.CurrentRuntime.DeleteReference(_env, _handle).ThrowIfFailed();
            }
            else
            {
                _context.SynchronizationContext.Post(
                    () => _context.Runtime.DeleteReference(
                        _env, _handle).ThrowIfFailed(), allowSync: true);
            }
        }
    }

    ~JSReference() => Dispose(disposing: false);
}
