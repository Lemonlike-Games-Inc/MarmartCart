using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>Describes one exact structural or nested state transition in an input behavior preset.</summary>
    public readonly struct InputBehaviorPresetChange
    {
        internal InputBehaviorPresetChange(StateChangeKind kind,
            InputBehavior behavior = null, IStateMemberReplay source = null,
            StateMember member = default, bool structural = false,
            int index = -1, int destinationIndex = -1)
        {
            Kind = kind;
            Behavior = behavior;
            Source = source;
            Member = member;
            Structural = structural;
            Index = index;
            DestinationIndex = destinationIndex;
        }

        /// <summary>Gets the scalar or collection operation that committed.</summary>
        public StateChangeKind Kind { get; }
        /// <summary>Gets the affected authored behavior when one is identifiable.</summary>
        public InputBehavior Behavior { get; }
        /// <summary>Gets the replayable node that owns <see cref="Member"/>, when scalar replay is available.</summary>
        public IStateMemberReplay Source { get; }
        /// <summary>Gets the exact generated member token for a nested scalar transition.</summary>
        public StateMember Member { get; }
        /// <summary>Gets whether behavior topology or hook ordering changed.</summary>
        public bool Structural { get; }
        /// <summary>Gets the first affected source index, or -1 when no collection index applies.</summary>
        public int Index { get; }
        /// <summary>Gets the destination index of a move, or -1 for other operations.</summary>
        public int DestinationIndex { get; }
    }

    /// <summary>Receives one exact committed input-behavior preset transition.</summary>
    public delegate void InputBehaviorPresetChangedHandler(InputBehaviorPreset preset,
        in InputBehaviorPresetChange change);

    /// <summary>
    /// Shareable, project-asset container of <see cref="InputBehavior"/> entries that editors
    /// apply in bulk via <see cref="UniTextEditable.BehaviorPresets"/> — one asset defines a
    /// field archetype (chat composer, form field, password) reused across scenes. Each editor
    /// runs its own copies of the behaviors, so per-instance behavior state never leaks into the asset.
    /// </summary>
    /// <remarks>
    /// Mutating <see cref="Behaviors"/> raises <see cref="Changed"/>. Live editors replay leaf
    /// members into the matching runtime behavior and rebuild only the affected hook-order suffix
    /// for structural changes.
    /// </remarks>
    [CreateAssetMenu(fileName = "InputBehaviorPreset", menuName = UniTextMenu.CreateAsset.BehaviorPreset)]
    public partial class InputBehaviorPreset : ScriptableObject, IInputBehaviorChangeSink
    {
        /// <summary>Input behaviors applied in hook order.</summary>
        [SerializeField, StateList(nameof(ApplyBehaviorsChange), Owned = true,
            AllowNullItems = false, AllowDuplicateReferences = false)]
        [Tooltip("Input behaviors applied to every editor using this preset (validation, key bindings, formatting, clipboard policy).")]
        private TypedList<InputBehavior> behaviors = new();

        [NonSerialized] private ReferenceBinding<InputBehavior> boundBehaviors;
        [NonSerialized] private bool dependenciesBound;
        [NonSerialized] private int version;

        /// <summary>
        /// Raised whenever the preset collection or one of its behavior nodes changes. Generated
        /// serialized reconciliation and the public mutation methods use this same event.
        /// </summary>
        public event Action Changed;

        /// <summary>Raised with exact affected-behavior, member, operation, and replay context.</summary>
        public event InputBehaviorPresetChangedHandler DeltaChanged;

        /// <summary>Count of transitions published so far; equal readings bracket a window in which nothing changed.</summary>
        internal int Version => version;

        private void OnEnable()
        {
            dependenciesBound = true;
            if (behaviors == null) SetBehaviorsState(new TypedList<InputBehavior>());
            else
            {
                BindBehaviors();
            }
        }

        private void OnDisable()
        {
            dependenciesBound = false;
            UnbindBehaviors();
        }

        private void ApplyBehaviorsChange(in StateListMutation<InputBehavior> mutation)
        {
            if (dependenciesBound) BindBehaviors();
            if (!mutation.TryGetCurrentItem(out var behavior) &&
                mutation.Kind == StateListMutationKind.Remove)
                behavior = mutation.PreviousItem;
            var change = new InputBehaviorPresetChange(mutation.ChangeKind,
                behavior, structural: true,
                index: mutation.AffectedIndex,
                destinationIndex: mutation.DestinationIndex);
            PublishChange(in change);
        }

        private void BindBehaviors()
        {
            boundBehaviors ??= new ReferenceBinding<InputBehavior>(ConnectBehavior, DisconnectBehavior);
            boundBehaviors.Reconcile(behaviors);
        }

        /// <summary>Copies every behavior of this preset for one editor: detached managed objects the asset never observes, sharing managed references only among themselves.</summary>
        internal InputBehavior[] CreateRuntimeBehaviors()
        {
            StateListRules.ValidateReferences(behaviors, false, false, nameof(behaviors));
            var context = new StateCopyContext();
            var result = new InputBehavior[behaviors.Count];
            for (var i = 0; i < result.Length; i++) result[i] = context.Copy(behaviors[i]);
            return result;
        }

        private void UnbindBehaviors() => boundBehaviors?.Clear();

        private void ConnectBehavior(InputBehavior behavior) => behavior.SetChangeSink(this);

        private void DisconnectBehavior(InputBehavior behavior)
        {
            if (ReferenceEquals(behavior.ChangeSink, this)) behavior.SetChangeSink(null);
        }

        void IInputBehaviorChangeSink.MarkInputBehaviorChanged(InputBehavior behavior,
            IStateMemberReplay source, StateMember member, bool structural)
        {
            var change = new InputBehaviorPresetChange(StateChangeKind.Value, behavior,
                source, member, structural);
            PublishChange(in change);
        }

        private void PublishChange(in InputBehaviorPresetChange change)
        {
            version++;
            DeltaChanged?.Invoke(this, in change);
            Changed?.Invoke();
        }

    }
}
