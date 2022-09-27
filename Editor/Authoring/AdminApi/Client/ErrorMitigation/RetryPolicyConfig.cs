using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Client.ErrorMitigation
{
    internal delegate Exception ExceptionPredicate(Exception ex);

    /// <summary>
    /// Retry Policy Config is a class that represents the values required for
    /// our Retry Policy which include default values as well as setters to
    /// clamp them into a sensible range.
    /// </summary>
    internal class RetryPolicyConfig
    {
        float _jitterMagnitude = 1.0f;
        float _delayScale = 1.0f;
        float _maxDelayTime = 8.0f;        
        List<ExceptionPredicate> _exceptionsToHandle = new List<ExceptionPredicate>();

        /// <summary>
        /// The Maximum number of Retries.
        /// </summary>
        public uint MaxRetries { get; set; } = 4;

        /// <summary>
        /// The Jitter Magnitude to help prevent a service from being
        /// overloaded with retry requests at regular intervals.
        /// </summary>
        /// <remarks>
        /// When set, the value is clamped between 0.001f and 1.0f
        /// </remarks>
        public float JitterMagnitude
        {
            get => _jitterMagnitude;
            set => _jitterMagnitude = Mathf.Clamp(value, 0.001f, 1.0f);
        }

        /// <summary>
        /// The Delay Scale that is used to calculate the time between each
        /// retry.
        /// </summary>
        /// <remarks>
        /// When set, the value is clamped between 0.05f and 1.0f.
        /// </remarks>
        public float DelayScale
        {
            get => _delayScale;
            set => _delayScale = Mathf.Clamp(value, 0.05f, 1.0f);
        }

        /// <summary>
        /// The Max Delay time between each retry.
        /// </summary>
        /// <remarks>
        /// When set, the value is clamped between 100 milliseconds and 60 seconds.
        /// </remarks>
        public float MaxDelayTime
        {
            get => _maxDelayTime;
            set => _maxDelayTime = Mathf.Clamp(value, 0.1f, 60.0f);
        }

        /// <summary>
        /// Registers an exception of the given type to retry with.
        /// </summary>
        /// <typeparam name="TException">The Exception type that we should catch and retry on.</typeparam>
        public void HandleException<TException>() where TException : Exception
        {
            _exceptionsToHandle.Add(exception => exception is TException ? exception : null);
        }

        /// <summary>
        /// Overload that allows registering of an exception as well as setting
        /// a additional condition function that will be checked if this
        /// exception is thrown.
        /// </summary>
        /// <typeparam name="TException">The Exception type that we should catch and retry on.</typeparam>
        /// <param name="condition">A condition function  for specifying additional behaviour when checking if we should retry with this exception type.</param>
        public void HandleException<TException>(Func<TException, bool> condition) where TException : Exception
        {
            _exceptionsToHandle.Add(exception => exception is TException tException && condition(tException) ? exception : null);
        }

        /// <summary>
        /// Helper function for checking if a particular exception is actually
        /// in the list of exceptions that we should handle.
        /// </summary>
        /// <param name="e">The exception that was triggered that we want to check.</param>
        /// <returns>Returns true if the exception is in the list of handled exceptions.</returns>
        public bool IsHandledException(Exception e)
        {
            if (_exceptionsToHandle != null)
            {
                foreach (var predicate in _exceptionsToHandle)
                {
                    var b = predicate(e);
                    if (b == e)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
