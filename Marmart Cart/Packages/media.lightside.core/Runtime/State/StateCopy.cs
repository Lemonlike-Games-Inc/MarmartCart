using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Generated copy surface of one serialized state owner: a detached instance of the same runtime
    /// type carrying the same serialized state, and an in-place replay from a same-type source.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IStateCopyable
    {
        /// <summary>
        /// Creates a detached instance of this instance's runtime type carrying its serialized state.
        /// Managed references copy through <paramref name="context"/>, Unity objects and strings are
        /// shared, non-serialized state stays default.
        /// </summary>
        object CreateStateCopy(StateCopyContext context);

        /// <summary>
        /// Replays every serialized member of the same-type <paramref name="source"/> through this
        /// instance's generated transitions: nested state of matching runtime type replays in place,
        /// anything else is replaced by a copy.
        /// </summary>
        void CopyStateFrom(object source, StateCopyContext context);
    }

    /// <summary>
    /// Identity of a node that its copies carry, so a list replay pairs each source item with the
    /// target item denoting the same node even after the list was reordered.
    /// </summary>
    public interface IStateIdentity
    {
        /// <summary>Whether <paramref name="other"/> denotes the same node as this instance.</summary>
        bool SameStateIdentity(object other);
    }

    /// <summary>
    /// One copy operation. A managed reference resolves once per operation, so references shared
    /// inside the copied graph stay shared in the result.
    /// </summary>
    public sealed class StateCopyContext
    {
        private Dictionary<object, object> copies;

        /// <summary>Returns this operation's copy of <paramref name="source"/>, creating it on first sight; null, strings and Unity objects return as they are.</summary>
        public T Copy<T>(T source) where T : class
        {
            if (source == null || source is string || source is UnityEngine.Object) return source;
            if (copies != null && copies.TryGetValue(source, out var existing)) return (T)existing;
            return (T)(source is IStateCopyable copyable
                ? copyable.CreateStateCopy(this)
                : StateCopy.CopyForeign(source, this));
        }

        /// <summary>Records <paramref name="copy"/> as the result for <paramref name="source"/> before the copy's members are filled, so references back to the source resolve to it.</summary>
        public void Register(object source, object copy)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (copy == null) throw new ArgumentNullException(nameof(copy));
            copies ??= new Dictionary<object, object>(ReferenceIdentityComparer<object>.Instance);
            copies[source] = copy;
        }

        /// <summary>Replays <paramref name="source"/> into <paramref name="target"/> when both are live instances of one generated type; otherwise leaves the target untouched and returns false.</summary>
        public bool TryReplay<T>(T target, T source) where T : class
        {
            if (ReferenceEquals(target, source)) return true;
            if (target == null || source == null || target.GetType() != source.GetType() ||
                target is not IStateCopyable copyable) return false;
            copyable.CopyStateFrom(source, this);
            return true;
        }

        /// <summary>
        /// Aligns a generated list of managed items with <paramref name="source"/>. Each source item is
        /// paired with the target item of the same node (<see cref="IStateIdentity"/>) or, without an
        /// identity, with the same-type item at its position; a paired item replays in place, any other
        /// gets a copy, and the list is then reordered, filled and trimmed by exact structural
        /// transitions.
        /// </summary>
        public void ReplayItems<T>(StateList<T> target, IReadOnlyList<T> source) where T : class
            => ReplayReferenceItems(target, source);

        internal void ReplayReferenceItems<T>(StateList<T> target, IReadOnlyList<T> source)
        {
            var count = source?.Count ?? 0;
            var paired = count == 0 ? Array.Empty<T>() : new T[count];
            var taken = target.Count == 0 ? Array.Empty<bool>() : new bool[target.Count];
            for (var i = 0; i < count; i++)
            {
                var item = source[i];
                if (item == null) continue;
                var index = item is IStateIdentity identity
                    ? FindIdentity(target, taken, identity, item)
                    : FindPositional(target, taken, i, item);
                if (index < 0)
                {
                    paired[i] = StateCopy.CopyAny(item, this);
                    continue;
                }
                taken[index] = true;
                var existing = target[index];
                paired[i] = StateCopy.TryReplayAny(existing, item, this)
                    ? existing
                    : StateCopy.CopyAny(item, this);
            }

            for (var i = 0; i < count; i++)
            {
                var desired = paired[i];
                if (i < target.Count && ReferenceEquals(target[i], desired)) continue;
                var existingIndex = -1;
                for (var j = i + 1; j < target.Count; j++)
                {
                    if (!ReferenceEquals(target[j], desired)) continue;
                    existingIndex = j;
                    break;
                }
                if (existingIndex >= 0)
                {
                    target.Move(existingIndex, i);
                    continue;
                }
                if (i < target.Count && !WantedLater(target[i], paired, i)) target.Replace(i, desired);
                else target.Insert(i, desired);
            }
            for (var i = target.Count - 1; i >= count; i--) target.RemoveAt(i);
        }

        private static int FindIdentity<T>(StateList<T> target, bool[] taken, IStateIdentity identity, T item)
        {
            for (var i = 0; i < target.Count; i++)
            {
                var candidate = target[i];
                if (taken[i] || candidate == null || candidate.GetType() != item.GetType() ||
                    !identity.SameStateIdentity(candidate)) continue;
                return i;
            }
            return -1;
        }

        private static int FindPositional<T>(StateList<T> target, bool[] taken, int index, T item)
        {
            if (index >= target.Count || taken[index]) return -1;
            var candidate = target[index];
            return candidate != null && candidate.GetType() == item.GetType() ? index : -1;
        }

        private static bool WantedLater<T>(T item, T[] paired, int index)
        {
            for (var i = index + 1; i < paired.Length; i++)
                if (ReferenceEquals(paired[i], item)) return true;
            return false;
        }
    }

    /// <summary>
    /// Copies serialized state graphs without Unity serialization: generated copiers for state owners,
    /// registered copiers for classes backed by a native handle, reflection over Unity-serialized
    /// fields for every other class.
    /// </summary>
    public static class StateCopy
    {
        private static readonly object gate = new();
        private static readonly object skip = new();
        private static readonly Dictionary<Type, Func<object, object>> registered = new();
        private static readonly Dictionary<Type, ReflectiveLayout> layouts = new();

        static StateCopy()
        {
            Register<AnimationCurve>(static source => new AnimationCurve(source.keys)
            {
                preWrapMode = source.preWrapMode,
                postWrapMode = source.postWrapMode,
            });
        }

        /// <summary>Creates a detached copy of <paramref name="source"/> in a fresh operation.</summary>
        public static T Copy<T>(T source) where T : class => new StateCopyContext().Copy(source);

        /// <summary>Replays <paramref name="source"/> into <paramref name="target"/> in a fresh operation; false when the two are not live instances of one generated type.</summary>
        public static bool TryReplay<T>(T target, T source) where T : class
            => new StateCopyContext().TryReplay(target, source);

        /// <summary>
        /// Declares how instances of <typeparamref name="T"/> copy. Required for a class whose state
        /// lives behind a native handle, which reflection refuses to duplicate; a later registration
        /// replaces an earlier one.
        /// </summary>
        public static void Register<T>(Func<T, T> copier) where T : class
        {
            if (copier == null) throw new ArgumentNullException(nameof(copier));
            lock (gate) registered[typeof(T)] = source => copier((T)source);
        }

        /// <summary>Copies every Unity-serialized field of <paramref name="source"/> into a new instance of its runtime type, resolving nested references through <paramref name="context"/>.</summary>
        public static object CopyByReflection(object source, StateCopyContext context)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var layout = LayoutOf(source.GetType());
            var copy = layout.CreateInstance();
            context.Register(source, copy);
            layout.CopyFields(source, copy, context);
            return copy;
        }

        /// <summary>Fills an empty list with <paramref name="items"/> without publishing a mutation and returns it.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static TList FillList<TList, T>(TList list, T[] items) where TList : class, IList<T>
        {
            if (list is IStateCollectionMutationSink<T> sink) sink.ReplaceStateContents(items, items.Length);
            else
                for (var i = 0; i < items.Length; i++)
                    list.Add(items[i]);
            return list;
        }

        /// <summary>Copies a value whose static type is a generic parameter: value types by value or detached snapshot, reference types through <paramref name="context"/>.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static T CopyAny<T>(T value, StateCopyContext context)
        {
            if (typeof(T).IsValueType)
                return value is IStateSnapshot<T> snapshot ? snapshot.CaptureStateSnapshot() : value;
            return (T)context.Copy((object)value);
        }

        /// <summary>Replays a value whose static type is a generic parameter; false when the value must be assigned instead.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TryReplayAny<T>(T target, T source, StateCopyContext context)
            => !typeof(T).IsValueType && context.TryReplay((object)target, (object)source);

        /// <summary>Aligns a generated list whose element type is a generic parameter with <paramref name="source"/>.</summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static void ReplayAnyItems<T>(StateList<T> target, IReadOnlyList<T> source,
            StateCopyContext context)
        {
            if (!typeof(T).IsValueType && typeof(T) != typeof(string))
            {
                context.ReplayReferenceItems(target, source);
                return;
            }
            var count = source?.Count ?? 0;
            var items = count == 0 ? Array.Empty<T>() : new T[count];
            for (var i = 0; i < count; i++) items[i] = CopyAny(source[i], context);
            target.ReplaceAll(items);
        }

        internal static object CopyForeign(object source, StateCopyContext context)
        {
            Func<object, object> copier;
            lock (gate) registered.TryGetValue(source.GetType(), out copier);
            if (copier == null) return CopyByReflection(source, context);
            var copy = copier(source) ?? throw new InvalidOperationException(
                $"The copier registered for '{source.GetType().FullName}' returned null.");
            context.Register(source, copy);
            return copy;
        }

        private static ReflectiveLayout LayoutOf(Type type)
        {
            lock (gate)
            {
                if (!layouts.TryGetValue(type, out var layout))
                    layouts.Add(type, layout = new ReflectiveLayout(type));
                return layout;
            }
        }

        private static object CopyValue(Type declaredType, object value, StateCopyContext context)
        {
            if (value == null) return null;
            if (typeof(Delegate).IsAssignableFrom(declaredType) ||
                typeof(IDictionary).IsAssignableFrom(declaredType)) return skip;
            var type = value.GetType();
            if (type.IsValueType)
                return LayoutOf(type).HoldsCopyableReferences ? CopyStruct(type, value, context) : value;
            if (value is string || value is UnityEngine.Object) return value;
            if (value is Array array) return array.Rank == 1 ? CopyArray(array, context) : skip;
            if (value is IList list && type.IsGenericType) return CopyList(list, type, context);
            return context.Copy(value);
        }

        private static object CopyStruct(Type type, object boxed, StateCopyContext context)
        {
            LayoutOf(type).CopyFields(boxed, boxed, context);
            return boxed;
        }

        private static Array CopyArray(Array source, StateCopyContext context)
        {
            var elementType = source.GetType().GetElementType();
            var copy = Array.CreateInstance(elementType, source.Length);
            context.Register(source, copy);
            for (var i = 0; i < source.Length; i++)
            {
                var item = CopyValue(elementType, source.GetValue(i), context);
                if (!ReferenceEquals(item, skip)) copy.SetValue(item, i);
            }
            return copy;
        }

        private static IList CopyList(IList source, Type type, StateCopyContext context)
        {
            var copy = (IList)Activator.CreateInstance(type, true);
            context.Register(source, copy);
            var elementType = typeof(object);
            foreach (var contract in type.GetInterfaces())
            {
                if (!contract.IsGenericType || contract.GetGenericTypeDefinition() != typeof(IList<>)) continue;
                elementType = contract.GetGenericArguments()[0];
                break;
            }
            for (var i = 0; i < source.Count; i++)
            {
                var item = CopyValue(elementType, source[i], context);
                copy.Add(ReferenceEquals(item, skip) ? null : item);
            }
            return copy;
        }

        private static bool IsSerialized(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral || field.IsNotSerialized) return false;
            if (field.IsPublic) return true;
            return field.IsDefined(typeof(SerializeField), false) ||
                   field.IsDefined(typeof(SerializeReference), false);
        }

        private static bool IsCopyableReference(Type type)
        {
            if (type.IsPointer || typeof(Delegate).IsAssignableFrom(type) ||
                typeof(IDictionary).IsAssignableFrom(type)) return false;
            if (type.IsValueType) return type.IsPrimitive || type.IsEnum ? false : LayoutOf(type).HoldsCopyableReferences;
            return type != typeof(string) && !typeof(UnityEngine.Object).IsAssignableFrom(type);
        }

        private sealed class ReflectiveLayout
        {
            private readonly Type type;
            private readonly FieldInfo[] fields;
            private readonly bool nativeHandle;
            private bool? holdsCopyableReferences;

            internal ReflectiveLayout(Type type)
            {
                this.type = type;
                var collected = new List<FieldInfo>();
                for (var current = type; current != null && current != typeof(object); current = current.BaseType)
                {
                    foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                            BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (field.IsStatic) continue;
                        if (field.FieldType == typeof(IntPtr) || field.FieldType == typeof(UIntPtr) ||
                            field.FieldType.IsPointer) nativeHandle = true;
                        if (IsSerialized(field)) collected.Add(field);
                    }
                }
                fields = collected.ToArray();
            }

            internal bool HoldsCopyableReferences
            {
                get
                {
                    if (holdsCopyableReferences is { } known) return known;
                    var holds = false;
                    for (var i = 0; i < fields.Length && !holds; i++)
                        holds = fields[i].FieldType != type && IsCopyableReference(fields[i].FieldType);
                    holdsCopyableReferences = holds;
                    return holds;
                }
            }

            internal object CreateInstance()
            {
                if (nativeHandle)
                    throw new NotSupportedException(
                        $"'{type.FullName}' keeps its state behind a native handle; register a copier through {nameof(StateCopy)}.{nameof(Register)}.");
                return Activator.CreateInstance(type, true);
            }

            internal void CopyFields(object source, object target, StateCopyContext context)
            {
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    var value = CopyValue(field.FieldType, field.GetValue(source), context);
                    if (!ReferenceEquals(value, skip)) field.SetValue(target, value);
                }
            }
        }
    }
}
