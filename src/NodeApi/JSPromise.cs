// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.JavaScript.NodeApi.Interop;
using static Microsoft.JavaScript.NodeApi.Runtime.JSRuntime;

namespace Microsoft.JavaScript.NodeApi;

/// <summary>
/// Represents a JavaScript Promise object.
/// </summary>
/// <seealso cref="TaskExtensions"/>
public readonly struct JSPromise : IJSValue<JSPromise>
{
    private readonly JSValue _value;

    /// <summary>
    /// Implicitly converts a <see cref="JSPromise" /> to a <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSPromise" /> to convert.</param>
    public static implicit operator JSValue(JSPromise promise) => promise._value;

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a nullable <see cref="JSPromise" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns>
    /// The <see cref="JSPromise" /> if it was successfully created or `null` if it was failed.
    /// </returns>
    public static explicit operator JSPromise?(JSValue value) => value.As<JSPromise>();

    /// <summary>
    /// Explicitly converts a <see cref="JSValue" /> to a <see cref="JSPromise" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to convert.</param>
    /// <returns><see cref="JSPromise" /> struct created based on this `JSValue`.</returns>
    /// <exception cref="InvalidCastException">
    /// Thrown when the T struct cannot be created based on this `JSValue`.
    /// </exception>
    public static explicit operator JSPromise(JSValue value) => value.CastTo<JSPromise>();

    public static explicit operator JSPromise(JSObject obj) => (JSPromise)(JSValue)obj;
    public static implicit operator JSObject(JSPromise promise) => (JSObject)promise._value;

    private JSPromise(JSValue value)
    {
        _value = value;
    }

    public delegate void ResolveCallback(Action<JSValue> resolve);

    public delegate Task AsyncResolveCallback(Action<JSValue> resolve);

    public delegate void ResolveRejectCallback(
        Action<JSValue> resolve,
        Action<JSError> reject);

    public delegate Task AsyncResolveRejectCallback(
        Action<JSValue> resolve,
        Action<JSError> reject);

    /// <summary>
    /// Creates a new JS Promise with a resolve callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/> or throw an exception.</param>
    /// <remarks>
    /// Any exception thrown by the callback will be caught and used as a promise rejection error.
    /// </remarks>
    public JSPromise(ResolveCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        try
        {
            callback(deferred.Resolve);
        }
        catch (Exception ex)
        {
            deferred.Reject(ex);
        }
    }

    /// <summary>
    /// Creates a new JS Promise with a resolve/reject callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/>, invoke the reject function with
    /// a JS Error, or throw an exception.</param>
    /// <remarks>
    /// Any exception thrown by the callback will be caught and used as a promise rejection error.
    /// </remarks>
    public JSPromise(ResolveRejectCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        try
        {
            callback(deferred.Resolve, deferred.Reject);
        }
        catch (Exception ex)
        {
            deferred.Reject(ex);
        }
    }

    /// <summary>
    /// Creates a new JS Promise with an async resolve callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/> or throw an exception.</param>
    /// <remarks>
    /// Any (sync or async) exception thrown by the callback will be caught and used as a promise
    /// rejection error.
    /// </remarks>
    public JSPromise(AsyncResolveCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        ResolveDeferred(callback, deferred);
    }

    private static async void ResolveDeferred(
        AsyncResolveCallback callback, Deferred deferred)
    {
        using var asyncScope = new JSAsyncScope();
        try
        {
            await callback(deferred.Resolve);
        }
        catch (Exception ex)
        {
            deferred.Reject(ex);
        }
    }

    /// <summary>
    /// Creates a new JS Promise with an async resolve/reject callback.
    /// </summary>
    /// <param name="callback">Callback that is invoked immediately and must _eventually_ invoke
    /// either the resolve function with a <see cref="JSValue"/>, invoke the reject function with
    /// a JS Error, or throw an exception.</param>
    /// <remarks>
    /// Any (sync or async) exception thrown by the callback will be caught and used as a promise
    /// rejection error.
    /// </remarks>
    public JSPromise(AsyncResolveRejectCallback callback)
    {
        _value = JSValue.CreatePromise(out Deferred deferred);
        ResolveDeferred(callback, deferred);
    }

    private static async void ResolveDeferred(
        AsyncResolveRejectCallback callback, Deferred deferred)
    {
        using var asyncScope = new JSAsyncScope();
        try
        {
            await callback(deferred.Resolve, deferred.Reject);
        }
        catch (Exception ex)
        {
            deferred.Reject(ex);
        }
    }

    #region IJSValue<JSPromise> implementation

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
    /// Determines whether a <see cref="JSPromise" /> can be created from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">The <see cref="JSValue" /> to check.</param>
    /// <returns>
    /// <c>true</c> if a <see cref="JSPromise" /> can be created from
    /// the specified <see cref="JSValue" />; otherwise, <c>false</c>.
    /// </returns>
#if NET7_0_OR_GREATER
    static bool IJSValue<JSPromise>.CanCreateFrom(JSValue value)
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static bool CanCreateFrom(JSValue value)
#pragma warning restore IDE0051
#endif
        => value.IsPromise();

    /// <summary>
    /// Creates a new instance of <see cref="JSPromise" /> from
    /// the specified <see cref="JSValue" />.
    /// </summary>
    /// <param name="value">
    /// The <see cref="JSValue" /> to create a <see cref="JSPromise" /> from.
    /// </param>
    /// <returns>
    /// A new instance of <see cref="JSPromise" /> created from
    /// the specified <see cref="JSValue" />.
    /// </returns>
#if NET7_0_OR_GREATER
    static JSPromise IJSValue<JSPromise>.CreateUnchecked(JSValue value) => new(value);
#else
#pragma warning disable IDE0051 // It is used by the IJSValueShim<T> class through reflection.
    private static JSPromise CreateUnchecked(JSValue value) => new(value);
#pragma warning restore IDE0051
#endif

    #endregion

    /// <summary>
    /// Registers callbacks that are invoked when a promise is fulfilled and/or rejected,
    /// and returns a new chained promise.
    /// </summary>
    public JSPromise Then(
        Func<JSValue, JSValue>? fulfilled,
        Func<JSError, JSValue>? rejected = null)
    {
        JSValue fulfilledFunction = fulfilled == null ? JSValue.Undefined :
            JSValue.CreateFunction(nameof(fulfilled), (args) => fulfilled(args[0]));
        JSValue rejectedFunction = rejected == null ? JSValue.Undefined :
            JSValue.CreateFunction(nameof(rejected), (args) => rejected(new JSError(args[0])));
        return (JSPromise)_value.CallMethod("then", fulfilledFunction, rejectedFunction);
    }

    /// <summary>
    /// Registers a callback that is invoked when a promise is rejected, and returns a new
    /// chained promise.
    /// </summary>
    public JSPromise Catch(Func<JSError, JSValue> rejected)
    {
        JSValue rejectedFunction = JSValue.CreateFunction(
            nameof(rejected), (args) => rejected(new JSError(args[0])));
        return (JSPromise)_value.CallMethod("catch", rejectedFunction);
    }

    /// <summary>
    /// Registers a callback that is invoked after a promise is fulfilled or rejected, and
    /// returns a new chained promise.
    /// </summary>
    public JSPromise Finally(Action completed)
    {
        JSValue completedFunction = JSValue.CreateFunction(nameof(completed), (_) =>
        {
            completed();
            return JSValue.Undefined;
        });
        return (JSPromise)_value.CallMethod("finally", completedFunction);
    }

    /// <summary>
    /// Creates a new promise that resolves to a value of `undefined`.
    /// </summary>
    public static JSPromise Resolve()
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise").CallMethod("resolve");
    }

    /// <summary>
    /// Creates a new promise that resolves to the provided value.
    /// </summary>
    public static JSPromise Resolve(JSValue value)
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise").CallMethod("resolve", value);
    }

    /// <summary>
    /// Creates a new promise that is rejected with the provided reason.
    /// </summary>
    public static JSPromise Reject(JSValue reason)
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise").CallMethod("reject", reason);
    }

    public static JSPromise All(params JSPromise[] promises) => Select("all", promises);

    public static JSPromise All(IEnumerable<JSPromise> promises) => Select("all", promises);

    public static JSPromise All(JSArray promises) => Select("all", promises);

    public static JSPromise Any(params JSPromise[] promises) => Select("any", promises);

    public static JSPromise Any(IEnumerable<JSPromise> promises) => Select("any", promises);

    public static JSPromise Any(JSArray promises) => Select("any", promises);

    public static JSPromise Race(params JSPromise[] promises) => Select("race", promises);

    public static JSPromise Race(IEnumerable<JSPromise> promises) => Select("race", promises);

    public static JSPromise Race(JSArray promises) => Select("race", promises);

    private static JSPromise Select(string operation, IEnumerable<JSPromise> promises)
    {
        JSArray promiseArray = new();
        foreach (JSPromise promise in promises) promiseArray.Add(promise);
        return Select(operation, promiseArray);
    }

    private static JSPromise Select(string operation, JSArray promiseArray)
    {
        return (JSPromise)JSRuntimeContext.Current.Import(null, "Promise")
            .CallMethod(operation, promiseArray);
    }

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator ==(JSPromise a, JSPromise b) => a._value.StrictEquals(b);

    /// <summary>
    /// Compares two JS values using JS "strict" equality.
    /// </summary>
    public static bool operator !=(JSPromise a, JSPromise b) => !a._value.StrictEquals(b);

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

    /// <summary>
    /// Supports resolving or rejecting a created JavaScript Promise.
    /// </summary>
    public struct Deferred
    {
        private napi_deferred _handle;

        internal Deferred(napi_deferred handle)
        {
            _handle = handle;
        }

        public readonly void Resolve(JSValue resolution)
        {
            // _handle becomes invalid after this call
            JSValueScope.CurrentRuntime.ResolveDeferred(
                (napi_env)JSValueScope.Current, _handle, (napi_value)resolution)
                .ThrowIfFailed();
        }

        public readonly void Reject(JSError rejection)
        {
            // _handle becomes invalid after this call
            JSValueScope.CurrentRuntime.RejectDeferred(
                (napi_env)JSValueScope.Current, _handle, (napi_value)rejection.Value)
                .ThrowIfFailed();
        }

        public readonly void Reject(Exception exception)
        {
            Reject(new JSError(exception));
        }
    }
}
