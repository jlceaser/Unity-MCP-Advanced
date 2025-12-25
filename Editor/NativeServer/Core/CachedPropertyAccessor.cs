using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace MCPForUnity.Editor.NativeServer.Core
{
    /// <summary>
    /// Provides cached, compiled property accessors using Expression trees.
    /// Much faster than reflection for repeated property access.
    /// </summary>
    public static class CachedPropertyAccessor
    {
        // Cache for compiled property getters
        private static readonly ConcurrentDictionary<(Type, string), Func<object, object>> _getterCache = new();

        // Cache for property existence checks
        private static readonly ConcurrentDictionary<(Type, string), bool> _propertyExistsCache = new();

        /// <summary>
        /// Gets a property value from an object using a cached compiled accessor.
        /// Returns null if property doesn't exist.
        /// </summary>
        public static object GetPropertyValue(object instance, string propertyName)
        {
            if (instance == null) return null;

            var type = instance.GetType();
            var key = (type, propertyName);

            // First check if property exists (cached)
            if (!_propertyExistsCache.TryGetValue(key, out bool exists))
            {
                exists = FindProperty(type, propertyName) != null;
                _propertyExistsCache[key] = exists;
            }

            if (!exists) return null;

            // Get or create compiled getter
            var getter = _getterCache.GetOrAdd(key, k => CreateGetter(k.Item1, k.Item2));
            return getter?.Invoke(instance);
        }

        /// <summary>
        /// Tries to get a boolean property value.
        /// Returns (true, value) if property exists and is bool, (false, default) otherwise.
        /// </summary>
        public static (bool found, bool value) TryGetBoolProperty(object instance, string propertyName)
        {
            var result = GetPropertyValue(instance, propertyName);
            if (result is bool boolValue)
            {
                return (true, boolValue);
            }
            return (false, false);
        }

        /// <summary>
        /// Tries to get a string property value.
        /// Returns (true, value) if property exists, (false, null) otherwise.
        /// </summary>
        public static (bool found, string value) TryGetStringProperty(object instance, string propertyName)
        {
            var result = GetPropertyValue(instance, propertyName);
            if (result != null)
            {
                return (true, result.ToString());
            }
            return (false, null);
        }

        /// <summary>
        /// Check if a property exists (with caching)
        /// </summary>
        public static bool HasProperty(Type type, string propertyName)
        {
            var key = (type, propertyName);
            return _propertyExistsCache.GetOrAdd(key, k => FindProperty(k.Item1, k.Item2) != null);
        }

        private static PropertyInfo FindProperty(Type type, string propertyName)
        {
            // Try exact match first
            var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null) return prop;

            // Try case-insensitive match
            return type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        }

        private static Func<object, object> CreateGetter(Type type, string propertyName)
        {
            var property = FindProperty(type, propertyName);
            if (property == null || !property.CanRead)
            {
                return null;
            }

            try
            {
                // Create: (object instance) => (object)((TypeName)instance).PropertyName
                var instanceParam = Expression.Parameter(typeof(object), "instance");
                var castInstance = Expression.Convert(instanceParam, type);
                var propertyAccess = Expression.Property(castInstance, property);
                var castResult = Expression.Convert(propertyAccess, typeof(object));

                var lambda = Expression.Lambda<Func<object, object>>(castResult, instanceParam);
                return lambda.Compile();
            }
            catch
            {
                // Fallback to reflection if Expression tree fails
                return instance => property.GetValue(instance);
            }
        }

        /// <summary>
        /// Pre-warm the cache for common types
        /// </summary>
        public static void PrewarmCache(params Type[] types)
        {
            string[] commonProps = { "success", "Success", "error", "Error", "message", "Message" };

            foreach (var type in types)
            {
                foreach (var prop in commonProps)
                {
                    var key = (type, prop);
                    if (!_getterCache.ContainsKey(key))
                    {
                        var propInfo = FindProperty(type, prop);
                        if (propInfo != null)
                        {
                            _propertyExistsCache[key] = true;
                            _getterCache[key] = CreateGetter(type, prop);
                        }
                        else
                        {
                            _propertyExistsCache[key] = false;
                        }
                    }
                }
            }
        }
    }
}
