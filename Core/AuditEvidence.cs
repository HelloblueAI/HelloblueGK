using System;
using System.Collections.Generic;
using System.Reflection;

namespace HB_NLP_Research_Lab.Core
{
    /// <summary>
    /// Evidence supplied by the operating organisation for a quality, security, or
    /// compliance audit.
    ///
    /// This repository contains no source of such evidence. The audit systems used to
    /// assert their own inputs — every boolean set to <c>true</c> and every metric set to
    /// a passing constant — and then grade those inputs against thresholds, which made a
    /// passing verdict unconditional. They now read their inputs from an instance of this
    /// class, and an absent key yields <c>false</c>, <c>0</c>, or an empty string, so an
    /// audit with no evidence fails its thresholds rather than passing them.
    ///
    /// Keys are <c>"Standard.PropertyName"</c>, matching the property names on the audit
    /// types — for example <c>"AS9100.QualityManagementSystem"</c> or
    /// <c>"Software.CodeCoverage"</c>.
    /// </summary>
    public sealed class AuditEvidence
    {
        private readonly Dictionary<string, bool> _attested;
        private readonly Dictionary<string, double> _measured;
        private readonly Dictionary<string, string> _recorded;

        public AuditEvidence()
        {
            _attested = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            _measured = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            _recorded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>An evidence set containing nothing. Every audit built from it fails.</summary>
        public static AuditEvidence None => new();

        /// <summary>True when no evidence of any kind has been supplied.</summary>
        public bool IsEmpty => _attested.Count == 0 && _measured.Count == 0 && _recorded.Count == 0;

        public AuditEvidence Attest(string key, bool value = true)
        {
            _attested[Require(key)] = value;
            return this;
        }

        public AuditEvidence Measure(string key, double value)
        {
            _measured[Require(key)] = value;
            return this;
        }

        public AuditEvidence Record(string key, string value)
        {
            _recorded[Require(key)] = value ?? string.Empty;
            return this;
        }

        public bool Attested(string key) => _attested.TryGetValue(Require(key), out var v) && v;

        public double Measured(string key) => _measured.TryGetValue(Require(key), out var v) ? v : 0.0;

        public string Recorded(string key) => _recorded.TryGetValue(Require(key), out var v) ? v : string.Empty;

        /// <summary>
        /// Builds an audit check of type <typeparamref name="T"/>, populating each writable
        /// property from the evidence under <c>"{standard}.{PropertyName}"</c>. Properties with
        /// no corresponding evidence keep the failing default, so the check's own
        /// <c>IsCompliant()</c> thresholds decide the outcome from supplied evidence alone.
        /// </summary>
        public T Build<T>(string standard) where T : new()
        {
            if (string.IsNullOrWhiteSpace(standard))
            {
                throw new ArgumentException("A standard name is required.", nameof(standard));
            }

            var check = new T();

            foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanWrite)
                {
                    continue;
                }

                var key = $"{standard}.{property.Name}";

                if (property.PropertyType == typeof(bool))
                {
                    property.SetValue(check, Attested(key));
                }
                else if (property.PropertyType == typeof(double))
                {
                    property.SetValue(check, Measured(key));
                }
                else if (property.PropertyType == typeof(int))
                {
                    property.SetValue(check, (int)Measured(key));
                }
                else if (property.PropertyType == typeof(string))
                {
                    property.SetValue(check, Recorded(key));
                }
            }

            return check;
        }

        private static string Require(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("An evidence key is required.", nameof(key));
            }

            return key;
        }
    }
}
