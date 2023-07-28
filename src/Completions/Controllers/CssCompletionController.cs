using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;

namespace TailwindCSSIntellisense.Completions.Controllers
{
    #region Command Filter

    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType("css")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class CssCompletionController : IVsTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdaptersFactory { get; set; }

        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView view = AdaptersFactory.GetWpfTextView(textViewAdapter);

            var filter = new CssCommandFilter(view, CompletionBroker);

            textViewAdapter.AddCommandFilter(filter, out var next);
            filter.Next = next;
        }
    }

    internal sealed class CssCommandFilter : IOleCommandTarget
    {
        ICompletionSession _currentSession;

        public CssCommandFilter(IWpfTextView textView, ICompletionBroker broker)
        {
            _currentSession = null;

            TextView = textView;
            Broker = broker;
        }

        public IWpfTextView TextView { get; private set; }
        public ICompletionBroker Broker { get; private set; }
        public IOleCommandTarget Next { get; set; }

        private char GetTypeChar(IntPtr pvaIn)
        {
            return (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Is the caret in an @apply context?
            bool isInApply = IsUsingAtDirective();
            string classText = null;
            if (isInApply)
            {
                // Text after @apply to caret
                classText = GetClassText();
            }

            bool handled = false;
            bool retrigger = false;
            int hresult = VSConstants.S_OK;

            // 1. Pre-process
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)nCmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        handled = StartSession(isInApply);
                        Filter();
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        handled = Complete(false);
                        break;
                    case VSConstants.VSStd2KCmdID.TAB:
                        handled = Complete(true);
                        if (isInApply)
                        {
                            retrigger = RetriggerIntellisense(classText);
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.CANCEL:
                        handled = Cancel();
                        break;
                }
            }

            if (!handled)
            {
                hresult = Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }

            if (retrigger)
            {
                StartSession(true);
            }

            if (ErrorHandler.Succeeded(hresult))
            {
                if (pguidCmdGroup == VSConstants.VSStd2K)
                {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID)
                    {
                        case VSConstants.VSStd2KCmdID.TYPECHAR:
                            var character = GetTypeChar(pvaIn);
                            if (character == ' ' || (isInApply && (CharsAfterSignificantPoint(classText) <= 1 || character == ':' || character == '/')))
                            {
                                _currentSession?.Dismiss();
                                StartSession(true);
                            }
                            else if (_currentSession != null)
                            {
                                DismissOtherSessions();
                                Filter();
                            }
                            break;
                        case VSConstants.VSStd2KCmdID.DELETEWORDLEFT:
                            _currentSession?.Dismiss();
                            StartSession(true);
                            break;
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                            // backspace is applied after this function is called, so this is actually
                            // equivalent to <= 1 (like above)
                            if (isInApply && (CharsAfterSignificantPoint(classText) <= 2 || classText.EndsWith("/")))
                            {
                                _currentSession?.Dismiss();
                                StartSession(true);
                            }
                            else if (_currentSession != null)
                            {
                                DismissOtherSessions();
                                Filter();
                            }
                            break;
                    }
                }
            }

            return hresult;
        }

        private int CharsAfterSignificantPoint(string classText)
        {
            var textAfterSignificantPoint = classText.Split(' ', ':').Last();

            return textAfterSignificantPoint.Length;
        }

        private bool RetriggerIntellisense(string classText)
        {
            return classText.EndsWith(":");
        }

        /// <summary>
        /// Dismisses the other sessions so two completion windows do not show up at once
        /// </summary>
        private void DismissOtherSessions()
        {
            foreach (var session in Broker.GetSessions(TextView))
            {
                if (session != _currentSession)
                {
                    session.Dismiss();
                }
            }
        }

        /// <summary>
        /// Narrow down the list of options as the user types input
        /// </summary>
        private void Filter()
        {
            if (_currentSession == null)
                return;

            _currentSession.SelectedCompletionSet.Filter();
            _currentSession.SelectedCompletionSet.SelectBestMatch();
        }

        /// <summary>
        /// Cancel the auto-complete session, and leave the text unmodified
        /// </summary>
        bool Cancel()
        {
            if (_currentSession == null)
                return false;

            _currentSession.Dismiss();

            return true;
        }

        /// <summary>
        /// Auto-complete text using the specified token
        /// </summary>
        bool Complete(bool force)
        {
            if (_currentSession == null)
                return false;

            if (!_currentSession.SelectedCompletionSet.SelectionStatus.IsSelected && !force)
            {
                _currentSession.Dismiss();
                return false;
            }
            else
            {
                var completionText = _currentSession.SelectedCompletionSet.SelectionStatus.Completion.InsertionText;
                // ) is for theme()
                var moveOneBack = completionText.EndsWith("]") || completionText.EndsWith(")");
                _currentSession.Commit();

                if (moveOneBack)
                {
                    TextView.Caret.MoveTo(TextView.Caret.Position.BufferPosition - 1);
                }

                return true;
            }
        }

        /// <summary>
        /// Display list of potential tokens
        /// </summary>
        bool StartSession(bool shouldStartNew = false)
        {
            if (_currentSession != null)
                return false;

            SnapshotPoint caret = TextView.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;

            var completionActive = Broker.IsCompletionActive(TextView);

            if (completionActive)
            {
                _currentSession = Broker.GetSessions(TextView)[0];
                _currentSession.Dismissed += (sender, args) => _currentSession = null;
            }
            else if (shouldStartNew)
            {
                _currentSession = Broker.CreateCompletionSession(TextView, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
                _currentSession.Dismissed += (sender, args) => _currentSession = null;
                _currentSession.Start();

                // Sometimes, creating another session will have irrelevant completions, so we need to filter
                Filter();
            }
            else
            {
                return false;
            }

            return true;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (pguidCmdGroup == VSConstants.VSStd2K)
            {
                switch ((VSConstants.VSStd2KCmdID)prgCmds[0].cmdID)
                {
                    case VSConstants.VSStd2KCmdID.AUTOCOMPLETE:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        prgCmds[0].cmdf = (uint)OLECMDF.OLECMDF_ENABLED | (uint)OLECMDF.OLECMDF_SUPPORTED;
                        return VSConstants.S_OK;
                }
            }
            return Next.QueryStatus(pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private bool IsUsingAtDirective()
        {
            var startPos = new SnapshotPoint(TextView.TextSnapshot, 0);
            var caretPos = TextView.Caret.Position.BufferPosition;

            var searchSnapshot = new SnapshotSpan(startPos, caretPos);
            var text = searchSnapshot.GetText();

            var lastIndexOfSemicolon = text.LastIndexOf(";");
            var lastIndexOfAt = text.LastIndexOf("@apply");

            return lastIndexOfAt != -1 && lastIndexOfAt > lastIndexOfSemicolon;
        }

        private string GetClassText()
        {
            var startPos = new SnapshotPoint(TextView.TextSnapshot, 0);
            var caretPos = TextView.Caret.Position.BufferPosition;

            var searchSnapshot = new SnapshotSpan(startPos, caretPos);
            var text = searchSnapshot.GetText();

            var lastIndexOfAt = text.LastIndexOf("@apply");

            return text.Substring(lastIndexOfAt + "@apply".Length).TrimStart();
        }
    }

    #endregion
}