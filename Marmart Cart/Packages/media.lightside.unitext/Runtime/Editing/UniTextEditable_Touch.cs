using System;
using UnityEngine;

namespace LightSide
{
    /// <summary>
    /// Touch layer of the editable: gesture recognition, selection / insertion handles,
    /// magnifier, mobile keyboard integration, and context-menu presentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Interaction follows the pointer, not the platform: a press whose <see cref="TextPointerEvent.Kind"/>
    /// is <see cref="PointerKind.Touch"/> opens a touch session and is routed through
    /// <see cref="TouchGestureRecognizer"/> — single/double/triple taps, long press and drag mapped to
    /// the platform text-editing conventions — while a mouse or pen press closes the session and runs
    /// the desktop click/drag handlers in <c>UniTextEditable_Standalone.cs</c>. A device offering both
    /// (a touch laptop, a tablet with a mouse) gets each pointer's own behaviour, and the presence of a
    /// touchscreen alone changes nothing.
    /// </para>
    /// <para>
    /// Selection handles (<see cref="ISelectionHandles"/> / <see cref="IInsertionHandle"/>) and
    /// the magnifier (<see cref="IMagnifier"/>) are entities owned by the sibling selectable.
    /// The editable drives them while each entity owns how its presentation is implemented.
    /// </para>
    /// </remarks>
    public partial class UniTextEditable
    {
        private ISelectionHandles selectionHandlesImpl;
        private IInsertionHandle insertionHandleImpl;
        private IMagnifier magnifierImpl;

        /// <summary>Gesture recognizer for tap/double-tap/long-press/drag detection; created by the first touch session.</summary>
        private TouchGestureRecognizer touchGesture;

        /// <summary>
        /// Set by the immediate <see cref="HandleTouchTapCaret"/> when a tap landed on the existing
        /// collapsed caret (moved nothing), read by the deferred <see cref="HandleTouchSingleTap"/> to
        /// raise the context menu — the two halves of one tap run a multi-tap window apart.
        /// </summary>
        private bool tapWasReTap;

        /// <summary>
        /// True while touch owns the field's pointer interaction: opened by a touch press, closed when a
        /// mouse / pen press or a defocus takes the field over. Gates the touch-UI policy — handles follow
        /// the selection, a selection change dismisses the menu — so a mouse session never shows touch chrome.
        /// </summary>
        private bool touchActive;

        /// <summary>Whether the assigned touch-UI slots have been resolved and subscribed — lazily, on the first touch session.</summary>
        private bool touchUIResolved;

        /// <summary>True from touch pointer-down to pointer-up; keeps the editable enrolled so the gesture recogniser's long-press timing ticks.</summary>
        private bool touchPointerActive;

        /// <summary>Resolved magnifier for the shipped handle components' loupe-over-handle drag; null when no magnifier is assigned.</summary>
        internal IMagnifier MagnifierImpl => magnifierImpl;

        /// <summary>
        /// Opens the touch session on a touch press: creates the gesture recogniser on first use and
        /// resolves the touch-UI slots (<see cref="EnsureTouchUI"/>). A no-op while a session is open.
        /// </summary>
        private void BeginTouchSession()
        {
            if (touchActive) return;
            touchActive = true;
            touchGesture ??= CreateTouchGesture();
            EnsureTouchUI();
        }

        private TouchGestureRecognizer CreateTouchGesture() => new()
        {
            CanvasSource = GetGestureCanvas,
            ThresholdsSource = GetGestureThresholds,
            OnTapCaret = HandleTouchTapCaret,
            OnSingleTap = HandleTouchSingleTap,
            OnDoubleTap = HandleTouchDoubleTap,
            OnTripleTap = HandleTouchTripleTap,
            OnLongPress = HandleTouchLongPress,
            OnLongPressEnd = HandleTouchLongPressEnd,
            OnDragStart = HandleTouchDragStart,
            OnDragUpdate = HandleTouchDragUpdate,
            OnDragEnd = HandleTouchDragEnd,
            OnScrollDrag = HandleTouchScrollDrag,
            OnScrollDragEnd = HandleTouchScrollDragEnd,
        };

        /// <summary>
        /// Closes the touch session: abandons the gesture in progress and hides every touch affordance.
        /// Runs when a mouse / pen press takes the field over and on defocus; a no-op without a session.
        /// </summary>
        private void EndTouchSession()
        {
            if (!touchActive) return;
            touchActive = false;
            touchPointerActive = false;
            reshowContextMenuPending = false;
            Selectable.EndDrag();
            HideAllTouchUI();
            touchGesture.Reset();
        }

        /// <summary>
        /// Tears down the touch layer. Called from OnDisable. The context menu is dismissed
        /// even without a touch session (the desktop right-click path presents it too), but only
        /// when this editable presented it — disabling a field must not hide a shared menu
        /// another field currently owns.
        /// </summary>
        private void TeardownTouch()
        {
            if (IsContextMenuVisible) HideContextMenu();
            if (touchGesture == null) return;

            touchActive = false;
            touchPointerActive = false;
            touchUIResolved = false;

            UnsubscribeTouchUI();
            if (Selectable != null) Selectable.TouchUISlotsChanged -= OnSelectableTouchUISlotsChanged;

            HideAllTouchUI();

            touchGesture.Reset();
            touchGesture = null;
        }

        /// <summary>
        /// Resolves the touch-UI slots on the first touch session from the sibling
        /// <see cref="UniTextSelectable"/> that owns them: casts its <see cref="UniTextSelectable.SelectionHandles"/>
        /// to <see cref="ISelectionHandles"/> / <see cref="IInsertionHandle"/> and its
        /// <see cref="UniTextSelectable.Magnifier"/> to <see cref="IMagnifier"/>, then subscribes to their
        /// drive events. Unassigned slots leave the corresponding touch UI absent.
        /// </summary>
        private void EnsureTouchUI()
        {
            if (touchUIResolved) return;
            touchUIResolved = true;

            ResolveTouchUISlots();
            if (Selectable != null) Selectable.TouchUISlotsChanged += OnSelectableTouchUISlotsChanged;

            SubscribeTouchUI();
        }

        /// <summary>Resolves the handle/magnifier implementations from the sibling
        /// <see cref="UniTextSelectable"/>, which owns the serialized touch-UI slots.</summary>
        private void ResolveTouchUISlots()
        {
            var handles = Selectable != null ? Selectable.SelectionHandles : null;
            selectionHandlesImpl = handles as ISelectionHandles;
            insertionHandleImpl = handles as IInsertionHandle;
            magnifierImpl = Selectable != null ? Selectable.Magnifier : null;
        }

        /// <summary>Rebinds handle subscriptions when the sibling's touch-UI slots change at runtime.</summary>
        private void OnSelectableTouchUISlotsChanged()
        {
            UnsubscribeHandles();
            UnsubscribeInsertionHandle();
            ResolveTouchUISlots();
            SubscribeHandles();
            SubscribeInsertionHandle();
        }

        /// <summary>
        /// Wires all touch UI events. Runs when the slots are resolved; <see cref="UnsubscribeTouchUI"/>
        /// mirrors it on disable, and every subscription is -=/+= paired so a repeat call
        /// can never stack duplicate handlers.
        /// </summary>
        private void SubscribeTouchUI()
        {
            SubscribeHandles();
            SubscribeInsertionHandle();
        }

        private void UnsubscribeTouchUI()
        {
            UnsubscribeHandles();
            UnsubscribeInsertionHandle();
        }

        private void SubscribeHandles()
        {
            if (selectionHandlesImpl == null) return;
            selectionHandlesImpl.AnchorDragged -= HandleAnchorHandleDragged;
            selectionHandlesImpl.FocusDragged -= HandleFocusHandleDragged;
            selectionHandlesImpl.SelectionHandleDragStarted -= HandleHandleDragStarted;
            selectionHandlesImpl.SelectionHandleDragEnded -= HandleHandleDragEnded;
            selectionHandlesImpl.AnchorDragged += HandleAnchorHandleDragged;
            selectionHandlesImpl.FocusDragged += HandleFocusHandleDragged;
            selectionHandlesImpl.SelectionHandleDragStarted += HandleHandleDragStarted;
            selectionHandlesImpl.SelectionHandleDragEnded += HandleHandleDragEnded;
        }

        private void UnsubscribeHandles()
        {
            if (selectionHandlesImpl == null) return;
            selectionHandlesImpl.AnchorDragged -= HandleAnchorHandleDragged;
            selectionHandlesImpl.FocusDragged -= HandleFocusHandleDragged;
            selectionHandlesImpl.SelectionHandleDragStarted -= HandleHandleDragStarted;
            selectionHandlesImpl.SelectionHandleDragEnded -= HandleHandleDragEnded;
        }

        private void SubscribeInsertionHandle()
        {
            if (insertionHandleImpl == null) return;
            insertionHandleImpl.InsertionHandleDragged -= HandleInsertionHandleDragged;
            insertionHandleImpl.InsertionHandleTapped -= HandleInsertionHandleTapped;
            insertionHandleImpl.InsertionHandleDragStarted -= HandleHandleDragStarted;
            insertionHandleImpl.InsertionHandleDragEnded -= HandleHandleDragEnded;
            insertionHandleImpl.InsertionHandleDragged += HandleInsertionHandleDragged;
            insertionHandleImpl.InsertionHandleTapped += HandleInsertionHandleTapped;
            insertionHandleImpl.InsertionHandleDragStarted += HandleHandleDragStarted;
            insertionHandleImpl.InsertionHandleDragEnded += HandleHandleDragEnded;
        }

        private void UnsubscribeInsertionHandle()
        {
            if (insertionHandleImpl == null) return;
            insertionHandleImpl.InsertionHandleDragged -= HandleInsertionHandleDragged;
            insertionHandleImpl.InsertionHandleTapped -= HandleInsertionHandleTapped;
            insertionHandleImpl.InsertionHandleDragStarted -= HandleHandleDragStarted;
            insertionHandleImpl.InsertionHandleDragEnded -= HandleHandleDragEnded;
        }

        /// <summary>
        /// Immediate half of a tap, fired on release before the multi-tap window resolves: activate /
        /// re-show the keyboard and place the caret at the tap, so the caret tracks the finger with no
        /// latency (iOS/Android behavior). A focusing tap activates and still places the caret; a tap on
        /// an active field with a dismissed keyboard re-shows it AND moves the caret. A tap that lands on
        /// the existing collapsed caret moves nothing and only arms the context-menu affordance for the
        /// deferred half. Superseded by a chained tap's word/line selection; neither half toggles focus off.
        /// </summary>
        private void HandleTouchTapCaret(Vector2 screenPosition)
        {
            tapWasReTap = false;

            bool focusingTap = !IsActive;
            if (focusingTap)
            {
                Activate();
                if (!IsActive) return;
            }
            else
            {
                Activate();
            }

            var camera = GetEventCamera();
            var codepointIndex = HitTestCaretSource(screenPosition, camera, out var upstream);
            bool caretMoved = codepointIndex != Selection.Focus || !Selection.IsCollapsed;

            if (!caretMoved && !focusingTap)
            {
                tapWasReTap = true;
                return;
            }

            Selectable.EndDrag();
            PlaceCaret(codepointIndex, upstream, SelectionChangeReason.Pointer);
            selectionHandlesImpl?.Hide();
        }

        /// <summary>
        /// Deferred half of a solitary tap, fired once the multi-tap window closes with no follow-up: shows
        /// the insertion handle, and — when the tap did not move the caret (a re-tap on the existing caret,
        /// or any tap on empty text) — toggles the context menu, the platform "tap the caret to summon or
        /// dismiss the menu" convention. Held back from <see cref="HandleTouchTapCaret"/> so a chained tap —
        /// which never reaches here — does not flash the handle/menu before its word selection replaces them.
        /// </summary>
        private void HandleTouchSingleTap(Vector2 screenPosition)
        {
            if (!IsActive) return;

            ShowInsertionHandleAtCaret();
            if (tapWasReTap)
                ToggleContextMenuForCurrentSelection();
        }

        /// <summary>
        /// Double tap: select word under tap, show selection handles and context menu.
        /// </summary>
        private void HandleTouchDoubleTap(Vector2 screenPosition)
        {
            if (!IsActive) return;

            var camera = GetEventCamera();
            var cluster = HitTestCaretSource(screenPosition, camera);
            ResetForGesture();
            Selectable.DispatchDoubleTap(cluster);

            ResolveTapSelectionUI();
        }

        /// <summary>
        /// Triple tap: select entire line/paragraph, show selection handles and context menu.
        /// </summary>
        private void HandleTouchTripleTap(Vector2 screenPosition)
        {
            if (!IsActive) return;

            var camera = GetEventCamera();
            var cluster = HitTestCaretSource(screenPosition, camera);
            ResetForGesture();
            Selectable.DispatchTripleTap(cluster);

            ResolveTapSelectionUI();
        }

        /// <summary>
        /// Long press: show magnifier for precise cursor placement. On Android, a press on a
        /// word selects it (the platform convention); on iOS long-press only places the caret
        /// under the magnifier — word selection is reserved for the double-tap.
        /// </summary>
        private void HandleTouchLongPress(Vector2 screenPosition)
        {
            if (!IsActive)
                Activate(showKeyboard: false);

            HideInsertionHandle();
            HideContextMenu();

            magnifierImpl?.Show(screenPosition);

            var camera = GetEventCamera();
            var codepointIndex = HitTestCaretSource(screenPosition, camera, out var upstream);

            if (LongPressSelectsWord() && codepointIndex >= 0 && codepointIndex < codepointCount && HasLayout)
            {
                var buffers = TextComponent.Buffers;
                if (buffers.codepoints.count > 0)
                {
                    var cpIndex = Mathf.Min(DocumentToRendered(codepointIndex), buffers.codepoints.count - 1);
                    var charClass = SelectionWordBreak.Classify(buffers.codepoints[cpIndex]);
                    if (charClass != WordCharClass.Whitespace && charClass != WordCharClass.Punctuation)
                    {
                        ResetForGesture();
                        Selectable.DispatchDoubleTap(codepointIndex);
                        return;
                    }
                }
            }

            PlaceCaret(codepointIndex, upstream, SelectionChangeReason.Pointer);
        }

        private static bool LongPressSelectsWord()
            => Application.platform != RuntimePlatform.IPhonePlayer;

        /// <summary>
        /// Long press ended: hide magnifier, show selection handles and toolbar if a word was selected.
        /// Ends the drag session the long-press path may have begun — the recognizer reports a
        /// long-press drag through OnLongPressEnd, never OnDragEnd, so without this the
        /// <c>wordDragMode</c>/<c>isDragging</c> state armed by a double-tap leaks into the next gesture.
        /// </summary>
        private void HandleTouchLongPressEnd(Vector2 screenPosition)
        {
            isDragging = false;
            dragMode = DragMode.None;
            magnifierImpl?.Hide();
            Selectable.EndDrag();

            if (!Selection.IsCollapsed)
            {
                ShowSelectionUI();
            }
            else
            {
                ShowInsertionHandleAtCaret();
                ShowContextMenuForCurrentSelection();
            }
        }

        /// <summary>
        /// Drag start: begin character-by-character or word-by-word selection.
        /// </summary>
        private void HandleTouchDragStart(Vector2 screenPosition)
        {
            if (!IsActive) return;

            HideContextMenu();
            magnifierImpl?.Show(screenPosition);

            var camera = GetEventCamera();
            dragMode = DragMode.Text;
            isDragging = true;
            lastDragScreenPosition = screenPosition;
            lastDragCamera = camera;
            var codepointIndex = HitTestCaretSource(screenPosition, camera, out var upstream);

            if (!Selectable.IsWordDragMode)
            {
                ResetForGesture();
            }
            Selectable.BeginDrag(codepointIndex, Selectable.IsWordDragMode,
                upstream ? CaretAffinity.Upstream : CaretAffinity.Downstream);
        }

        /// <summary>
        /// Drag update: extend selection to current position.
        /// </summary>
        private void HandleTouchDragUpdate(Vector2 screenPosition)
        {
            if (!IsActive) return;

            magnifierImpl?.UpdatePosition(screenPosition);
            UpdatePointerDrag(DragMode.Text, screenPosition, GetEventCamera());
        }

        /// <summary>
        /// Records a live pointer drag (which endpoint it moves, and where) and applies it once. The same
        /// (mode, position) is re-applied by <see cref="DragAutoScroll"/> each frame the pointer is held
        /// past a viewport edge, so every drag — text, either selection handle, the caret handle — extends
        /// through the one smooth auto-scroll path instead of the instant <see cref="EnsureCaretVisible"/>
        /// jump.
        /// </summary>
        private void UpdatePointerDrag(DragMode mode, Vector2 screenPosition, Camera camera)
        {
            dragMode = mode;
            isDragging = true;
            lastDragScreenPosition = screenPosition;
            lastDragCamera = camera;
            ApplyDragTo(screenPosition, camera);
        }

        /// <summary>Applies the active <see cref="dragMode"/> at a screen position: moves that endpoint to the
        /// hit-tested caret and refreshes the affected touch UI. Shared by the live drag handlers and the
        /// per-frame auto-scroll re-application.</summary>
        private void ApplyDragTo(Vector2 screenPosition, Camera camera)
        {
            var cp = HitTestCaretSource(screenPosition, camera, out var upstream);
            var affinity = upstream ? CaretAffinity.Upstream : CaretAffinity.Downstream;
            switch (dragMode)
            {
                case DragMode.AnchorHandle:
                    Selectable.DragSelectionHandle(true, cp, Selection.Affinity);
                    MarkSelectionDirty();
                    UpdateSelectionHandlePositions();
                    break;
                case DragMode.FocusHandle:
                    Selectable.DragSelectionHandle(false, cp, affinity);
                    MarkSelectionDirty();
                    UpdateSelectionHandlePositions();
                    break;
                case DragMode.Caret:
                    PlaceCaret(cp, upstream, SelectionChangeReason.Pointer);
                    insertionHandleImpl?.UpdatePosition();
                    break;
                default:
                    Selectable.UpdateDrag(cp, affinity);
                    MarkSelectionDirty();
                    break;
            }
        }

        /// <summary>
        /// Drag end: finalize selection, hide magnifier, show handles if selection exists.
        /// </summary>
        private void HandleTouchDragEnd()
        {
            isDragging = false;
            dragMode = DragMode.None;
            magnifierImpl?.Hide();
            Selectable.EndDrag();

            if (!Selection.IsCollapsed)
            {
                ShowSelectionUI();
            }
            else
            {
                selectionHandlesImpl?.Hide();
            }
        }

        private void HandleTouchScrollDrag(Vector2 delta)
        {
            if (!CanScroll) return;

            HideInsertionHandle();
            HideContextMenu();
            scrollOffset += new Vector2(delta.x, delta.y);
            ClampScrollOffset();
            ApplyScrollOffset();
            RefreshCaretVisual();
            UpdateSelectionHandlePositions();
        }

        private void HandleTouchScrollDragEnd()
        {
            if (!Selection.IsCollapsed && IsSelectionVisibleInViewport())
                ShowContextMenuForCurrentSelection();
        }

        private bool IsSelectionVisibleInViewport()
        {
            if (Selection.IsCollapsed) return false;
            var vr = GetViewportRect();
            var startVP = TextRectToViewport(CaretRectAtSource(Selection.Start));
            var endVP = TextRectToViewport(CaretRectAtSource(Selection.End));
            float selTop = Mathf.Max(startVP.yMax, endVP.yMax);
            float selBottom = Mathf.Min(startVP.yMin, endVP.yMin);
            return selBottom < vr.yMax && selTop > vr.yMin;
        }

        /// <summary>
        /// Called when the user drags the anchor selection handle.
        /// </summary>
        private void HandleAnchorHandleDragged(Vector2 screenPosition)
        {
            if (!IsActive) return;

            magnifierImpl?.UpdatePosition(screenPosition);
            UpdatePointerDrag(DragMode.AnchorHandle, screenPosition, GetEventCamera());
        }

        /// <summary>
        /// Called when the user drags the focus selection handle.
        /// </summary>
        private void HandleFocusHandleDragged(Vector2 screenPosition)
        {
            if (!IsActive) return;

            magnifierImpl?.UpdatePosition(screenPosition);
            UpdatePointerDrag(DragMode.FocusHandle, screenPosition, GetEventCamera());
        }

        private void HandleInsertionHandleTapped()
        {
            if (!IsActive) return;
            ToggleContextMenuForCurrentSelection();
        }

        /// <summary>Any handle drag started: remember whether the menu was up and hide it for the drag.</summary>
        private void HandleHandleDragStarted()
        {
            if (!IsActive) return;
            menuVisibleBeforeHandleDrag = IsContextMenuVisible;
            if (IsContextMenuVisible)
                HideContextMenu();
        }

        /// <summary>Any handle drag ended: end the auto-scroll drag, then bring the menu back over the new
        /// selection only if it was up when the drag began.</summary>
        private void HandleHandleDragEnded()
        {
            isDragging = false;
            dragMode = DragMode.None;
            if (!IsActive) return;
            if (menuVisibleBeforeHandleDrag)
                ShowContextMenuForCurrentSelection();
            menuVisibleBeforeHandleDrag = false;
        }

        private void ResetForGesture()
        {
            undoStack.BreakCoalescing();
            desiredX = float.NaN;
        }

        private void ShowSelectionUI()
        {
            ShowSelectionHandlesForCurrentSelection();
            ShowContextMenuForCurrentSelection();
        }

        /// <summary>
        /// Resolves touch UI after a tap-driven selection attempt. A range shows the selection handles and
        /// menu; a collapsed result — a word/line tap on empty or word-less text where nothing could be
        /// selected, i.e. the caret did not move — keeps the insertion handle at the caret and TOGGLES the
        /// context menu (the tap summons or dismisses it, the platform "tap the caret" convention).
        /// </summary>
        private void ResolveTapSelectionUI()
        {
            if (Selection.IsCollapsed)
            {
                ShowInsertionHandleAtCaret();
                ToggleContextMenuForCurrentSelection();
            }
            else
            {
                HideInsertionHandle();
                ShowSelectionUI();
            }
        }

        private void ShowSelectionHandlesForCurrentSelection()
        {
            if (selectionHandlesImpl == null || Selection.IsCollapsed) return;
            selectionHandlesImpl.Show();
        }

        private void UpdateSelectionHandlePositions()
        {
            if (!isActive || selectionHandlesImpl == null || Selection.IsCollapsed) return;
            selectionHandlesImpl.UpdatePositions();
        }

        /// <summary>Shows the resolved context menu for the current selection; the capabilities decide which items apply.</summary>
        private void ShowContextMenuForCurrentSelection()
            => ShowContextMenu(GetContextMenuScreenPosition());

        /// <summary>Toggles the context menu for the current selection: hides it if this field is presenting
        /// it, shows it otherwise — so repeating whatever gesture summons the menu also dismisses it.</summary>
        private void ToggleContextMenuForCurrentSelection()
        {
            if (IsContextMenuVisible) HideContextMenu();
            else ShowContextMenuForCurrentSelection();
        }

        private Action<ContextMenuAction> contextMenuPresenter;

        /// <summary>Set by a path that changes the selection and must end with the menu up (Select All from the menu, whose item click hides it right after the action; a context request on a touch pointer): the menu is presented on the next selection pass, after the change is applied, instead of being dismissed by it.</summary>
        private bool reshowContextMenuPending;
        private bool touchSelectionUpdatePending;

        /// <summary>Whether the context menu was up when the current handle drag began; the menu reappears on release only if it was.</summary>
        private bool menuVisibleBeforeHandleDrag;

        /// <summary>Canvas whose density scales the gesture recognizer's dp thresholds on displays reporting no dpi.</summary>
        private Canvas GetGestureCanvas()
            => TextComponent != null ? TextComponent.canvas : null;

        /// <summary>Recognizer thresholds from the settings asset — re-read per event so inspector edits apply immediately.</summary>
        private static GestureThresholds GetGestureThresholds() => new()
        {
            DragSlopDp = UniTextSettings.DragSlopDp,
            MultiTapSlopDp = UniTextSettings.MultiTapSlopDp,
            MultiTapWindow = UniTextSettings.MultiTapWindow,
            LongPressDuration = UniTextSettings.LongPressDuration,
        };

        private bool IsContextMenuVisible => Selectable?.IsContextMenuVisible(this) == true;

        /// <summary>Shows the menu with this editable as the presenter — the menu routes actions to whichever field last showed it, so a shared menu never leaks actions to defocused editors.</summary>
        private void ShowContextMenu(Vector2 screenPosition)
        {
            if (Selectable == null) return;
            var capabilities = BuildContextMenuCapabilities();
            Selectable.PresentContextMenu(screenPosition, in capabilities,
                contextMenuPresenter ??= OnContextMenuAction, this);
        }

        private void HideContextMenu()
        {
            Selectable?.DismissContextMenu(this);
        }

        private void OnContextMenuAction(ContextMenuAction action)
        {
            switch (action)
            {
                case ContextMenuAction.Cut: Cut(); break;
                case ContextMenuAction.Copy: Copy(); break;
                case ContextMenuAction.Paste: DispatchPaste(plain: false); break;
                case ContextMenuAction.SelectAll: SelectAll(); reshowContextMenuPending = true; break;
            }
        }

        private ContextMenuCapabilities BuildContextMenuCapabilities()
        {
            var hasSelection = !Selection.IsCollapsed;
            var canCopy = hasSelection && IsCopyAllowed();
            var canCut = canCopy && !readOnly;
            var canPaste = !readOnly && UniTextClipboard.HasContent();
            var canSelectAll = codepointCount > 0
                && !(Selection.Start == 0 && Selection.End == codepointCount);
            return new ContextMenuCapabilities(canCut, canCopy, canPaste, canSelectAll, hasSelection);
        }

        /// <summary>Screen-space anchor for the context menu: above the selection midpoint, or above the caret.</summary>
        private Vector2 GetContextMenuScreenPosition()
        {
            Vector2 anchor;
            if (!Selection.IsCollapsed)
            {
                var start = GetCaretScreenPosition(Selection.Start, false);
                var end = GetCaretScreenPosition(Selection.End, false);
                anchor = new Vector2((start.x + end.x) * 0.5f, Mathf.Max(start.y, end.y));
            }
            else
            {
                anchor = GetCaretScreenPosition(Selection.Focus, false);
            }

            var view = GetViewportScreenRect();
            anchor.x = Mathf.Clamp(anchor.x, view.xMin, view.xMax);
            anchor.y = Mathf.Clamp(anchor.y, view.yMin, view.yMax);
            return anchor;
        }

        /// <summary>
        /// Hides all touch UI elements (handles, context menu, magnifier).
        /// </summary>
        private void HideAllTouchUI()
        {
            selectionHandlesImpl?.Hide();
            HideContextMenu();
            magnifierImpl?.Hide();
            HideInsertionHandle();
        }

        private void HandleInsertionHandleDragged(Vector2 screenPosition)
        {
            if (!IsActive) return;

            HideContextMenu();
            UpdatePointerDrag(DragMode.Caret, screenPosition, GetEventCamera());
        }

        private bool insertionHandleVisible;

        private void ShowInsertionHandleAtCaret()
        {
            if (insertionHandleImpl == null) return;
            insertionHandleImpl.Show();
            insertionHandleVisible = true;
        }

        private void HideInsertionHandle()
        {
            insertionHandleVisible = false;
            insertionHandleImpl?.Hide();
        }

        private void UpdateInsertionHandleIfVisible()
        {
            if (!isActive || !insertionHandleVisible || insertionHandleImpl == null) return;
            insertionHandleImpl.UpdatePosition();
        }

        /// <summary>
        /// Context-menu entry from the core surface (<see cref="UniTextBase.ContextRequested"/>):
        /// right-click, pen long-press, and a touch long-press the core promoted because a pointer
        /// subscriber consumed the press (touch holds the recogniser saw never arrive here — the
        /// editable claims them through <see cref="UniTextBase.longPressClaimed"/> and presents its own
        /// long-press flow). Windows convention: a click outside the current selection moves the caret
        /// there first; a click inside keeps the selection. A mouse / pen request closes any touch session
        /// and opens the menu at the pointer; a touch request opens it over the selection once the caret
        /// change has been applied, as the touch gestures do.
        /// </summary>
        private void HandleContextRequested(TextPointerEvent evt)
        {
            if (evt.Consumed) return;
            if (evt.Kind == PointerKind.Touch) BeginTouchSession();
            else EndTouchSession();

            if (!IsActive) Activate();
            if (!IsActive) return;

            evt.Consumed = true;

            var cp = HitTestCaretSource(evt.ScreenPosition, evt.EventCamera, out var upstream);
            var sel = Selection;
            if (sel.IsCollapsed || cp < sel.Start || cp > sel.End)
                PlaceCaret(cp, upstream, SelectionChangeReason.Pointer);

            if (touchActive)
            {
                reshowContextMenuPending = true;
                MarkSelectionDirty();
            }
            else ShowContextMenu(evt.ScreenPosition);
        }

        /// <summary>
        /// Selection pass while a touch session is open: keeps the touch UI coherent with the new
        /// selection — handles follow a range, a collapse hides them — and the change dismisses the
        /// context menu unless a path asked for it to be (re)presented (<see cref="reshowContextMenuPending"/>).
        /// </summary>
        private void OnTouchSelectionChanged()
        {
            if (!touchActive) return;

            if (!Selection.IsCollapsed)
            {
                HideInsertionHandle();
                if (touchGesture.IsDragging || touchGesture.IsLongPressActive)
                    UpdateSelectionHandlePositions();
                else
                    ShowSelectionHandlesForCurrentSelection();
            }
            else
            {
                selectionHandlesImpl?.Hide();
            }

            if (reshowContextMenuPending)
                ShowContextMenuForCurrentSelection();
            else if (Selection.IsCollapsed)
                HideContextMenu();

            reshowContextMenuPending = false;
        }
    }
}
