// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Microsoft.JavaScript.NodeApi.Interop;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Enables creation of JS Proxy objects with C# handler callbacks.
/// </summary>
public readonly partial struct JSProxy : IJSValue<JSProxy>
{
    private readonly JSValue _value;
    private readonly JSValue _revoke = default;

    /// <summary>
    /// Implicitly converts a <see cref="JSProxy" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSProxy" /> to convert.</param>
    public static implicit operator JSValue(JSProxy proxy) => proxy._value;

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSProxy" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSProxy" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSProxy?(JSValue value) => value.As<JSProxy>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSProxy" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSProxy" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSProxy(JSValue value) => value.CastTo<JSProxy>();

    private JSProxy(JSValue value)
    {
        _value = value;
    }

    /// <summary>
    /// Creates a new JS proxy for a target.
    /// </summary>
    /// <param name="jsTarget">JS target for the proxy.</param>
    /// <param name="handler">Proxy handler callbacks (traps).</param>
    /// <param name="target">Optional target object to be wrapped by the JS target,
    /// or null if the JS target will not wrap anything.</param>
    /// <param name="revocable">True if the proxy may be revoked; defaults to false.</param>
    /// <remarks>
    /// If a wrapped target object is provided, proxy callbacks my access that object by calling
    /// <see cref="JSObject.Unwrap{T}"/>.
    /// </remarks>
    public JSProxy(
        JSObject jsTarget,
        Handler handler,
        object? target = null,
        bool revocable = false)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        if (target != null)
        {
            jsTarget.Wrap(target);
        }

        JSValue proxyConstructor = JSRuntimeContext.Current.Import(null, "Proxy");

        if (revocable)
        {
            JSValue proxyAndRevoke = proxyConstructor[nameof(revocable)]
                .Call(jsTarget, handler.JSHandler);
            _value = proxyAndRevoke["proxy"];
            _revoke = proxyAndRevoke["revoke"];
        }
        else
        {
            _value = proxyConstructor.CallAsConstructor(jsTarget, handler.JSHandler);
        }
    }

    #region IJSValue<JSProxy> implementation

    /// <summary>
    /// Checks if the T struct can be created from this instance`.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// `true` if the T struct can be created from this instance. Otherwise it returns `false`.
    /// </returns>
    public bool Is<T>() where T : struct, IJSValue<T> => _value.Is<T>();

    /// <summary>
    /// Tries to create a T struct from this instance.
    /// It returns `null` if the T struct cannot be created.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>
    /// Nullable value that contains T struct if it was successfully created
    /// or `null` if it was failed.
    /// </returns>
    public T? As<T>() where T : struct, IJSValue<T> => _value.As<T>();

    /// <summary>
    /// Creates a T struct from this instance without checking the enclosed handle type.
    /// It must be used only when the handle type is known to be correct.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    public T AsUnchecked<T>() where T : struct, IJSValue<T> => _value.AsUnchecked<T>();

    /// <summary>
    /// Creates a T struct from this instance.
    /// It throws `InvalidCastException` in case of failure.
    /// </summary>
    /// <typeparam name="T">A struct that implements IJSValue interface.</typeparam>
    /// <returns>T struct created based on this instance.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be crated based on this instance.
    /// </exception>
    public T CastTo<T>() where T : struct, IJSValue<T> => _value.CastTo<T>();

    /// <summary>
    /// Determines whether a <see cref="JSProxy" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSProxy" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
#if NET7_0_OR_GREATER
    static bool IJSValue<JSProxy>.CanCreateFrom(JSValue value)
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static bool CanCreateFrom(JSValue value)
#pragma warning restore IDE0051
#endif
    {
        // According to JavaScript specification we cannot differentiate Proxy instance
        // from other objects.
        return value.IsObject();
    }

    /// <summary>
    /// Creates a new instance of <see cref="JSProxy" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSProxy" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSProxy" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSProxy IJSValue<JSProxy>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSProxy CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    #endregion

    /// <summary>
    /// Revokes the proxy, so that further access to the target is no longer trapped by
    /// the proxy handler.
    /// </summary>
    /// <exception cref="InvalidOperationException">The proxy is not revocable.</exception>
    public void Revoke()
    {
        if (_revoke == default)
        {
            throw new InvalidOperationException("Proxy is not revokable.");
        }

        _revoke.Call();
    }

    public delegate JSValue Apply(JSObject target, JSValue thisArg, JSArray arguments);
    public delegate JSObject Construct(JSObject target, JSArray arguments, JSValue newTarget);
    public delegate bool DefineProperty(JSObject target, JSValue property, JSObject descriptor);
    public delegate bool DeleteProperty(JSObject target, JSValue property);
    public delegate JSValue Get(JSObject target, JSValue property, JSObject receiver);
    public delegate JSPropertyDescriptor GetOwnPropertyDescriptor(
        JSObject target, JSValue property);
    public delegate JSObject GetPrototypeOf(JSObject target);
    public delegate bool Has(JSObject target, JSValue property);
    public delegate bool IsExtensible(JSObject target);
    public delegate JSArray OwnKeys(JSObject target);
    public delegate bool PreventExtensions(JSObject target);
    public delegate bool Set(JSObject target, JSValue property, JSValue value, JSObject receiver);
    public delegate bool SetPrototypeOf(JSObject target, JSObject prototype);

    /// <summary>
    /// Specifies handler callbacks (traps) for a JS proxy.
    /// </summary>
    public sealed class Handler : IDisposable
    {
        public Handler(string? name = null)
        {
            Name = name;
            JSHandlerReference = new Lazy<JSReference>(
                () => new JSReference(CreateJSHandler()),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// Gets the name that was given to the handler (for diagnostic purposes),
        /// or null if no name was assigned.
        /// </summary>
        public string? Name { get; }

        private Lazy<JSReference> JSHandlerReference { get; }

        /// <summary>
        /// Gets the JS object with the callback methods defined on it.
        /// </summary>
        internal JSObject JSHandler => (JSObject)JSHandlerReference.Value.GetValue()!;

        public Apply? Apply { get; init; }
        public Construct? Construct { get; init; }
        public DefineProperty? DefineProperty { get; init; }
        public DeleteProperty? DeleteProperty { get; init; }
        public Get? Get { get; init; }
        public GetOwnPropertyDescriptor? GetOwnPropertyDescriptor { get; init; }
        public GetPrototypeOf? GetPrototypeOf { get; init; }
        public Has? Has { get; init; }
        public IsExtensible? IsExtensible { get; init; }
        public OwnKeys? OwnKeys { get; init; }
        public PreventExtensions? PreventExtensions { get; init; }
        public Set? Set { get; init; }
        public SetPrototypeOf? SetPrototypeOf { get; init; }

        private JSObject CreateJSHandler()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException($"{nameof(JSProxy)}.{nameof(Handler)}");
            }

            List<JSPropertyDescriptor> properties = new();

            if (Apply != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "apply",
                    (args) => Apply((JSObject)args[0], args[1], (JSArray)args[2])));
            }

            if (Construct != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "construct",
                    (args) => Construct((JSObject)args[0], (JSArray)args[1], args[2])));
            }

            if (DefineProperty != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "defineProperty",
                    (args) => DefineProperty((JSObject)args[0], args[1], (JSObject)args[2])));
            }

            if (DeleteProperty != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "deleteProperty",
                    (args) => DeleteProperty((JSObject)args[0], args[1])));
            }

            if (Get != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "get",
                    (args) => Get((JSObject)args[0], args[1], (JSObject)args[2])));
            }

            if (GetOwnPropertyDescriptor != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "getOwnPropertyDescriptor",
                    (args) => (JSObject)GetOwnPropertyDescriptor((JSObject)args[0], args[1])));
            }

            if (GetPrototypeOf != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "getPrototypeOf",
                    (args) => GetPrototypeOf((JSObject)args[0])));
            }

            if (Has != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "has",
                    (args) => Has((JSObject)args[0], args[1])));
            }

            if (IsExtensible != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "isExtensible",
                    (args) => IsExtensible((JSObject)args[0])));
            }

            if (OwnKeys != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "ownKeys",
                    (args) => OwnKeys((JSObject)args[0])));
            }

            if (PreventExtensions != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "preventExtensions",
                    (args) => PreventExtensions((JSObject)args[0])));
            }

            if (Set != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "set",
                    (args) => Set((JSObject)args[0], args[1], args[2], (JSObject)args[3])));
            }

            if (SetPrototypeOf != null)
            {
                properties.Add(JSPropertyDescriptor.Function(
                    "setPrototypeOf",
                    (args) => SetPrototypeOf((JSObject)args[0], (JSObject)args[1])));
            }

            var jsHandler = new JSObject();
            jsHandler.DefineProperties(properties.ToArray());
            return jsHandler;
        }

        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Disposes the proxy handler.
        /// </summary>
        /// <remarks>
        /// Disposing a proxy handler does not revoke or dispose proxies created using the handler.
        /// It does prevent new proxies from being created using the handler instance.
        /// </remarks>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                if (JSHandlerReference.IsValueCreated)
                {
                    JSHandlerReference.Value.Dispose();
                }
            }
        }

        public override string ToString()
        {
            return $"{nameof(JSProxy)}.{nameof(Handler)} \"{Name}\"";
        }
    }

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSProxy a, JSProxy b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSProxy a, JSProxy b) => !a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public bool Equals(JSValue other) => _value.StrictEquals(other);

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is JSValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        throw new NotSupportedException(
            "Hashing JS values is not supported. Use JSSet or JSMap instead.");
    }
}
