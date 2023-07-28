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
    [ContentType("razor")]
    [ContentType("LegacyRazorCSharp")]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    internal sealed class RazorCompletionController : IVsTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdaptersFactory { get; set; }

        [Import]
        internal ICompletionBroker CompletionBroker { get; set; }

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView view = AdaptersFactory.GetWpfTextView(textViewAdapter);

            var filter = new RazorCommandFilter(view, CompletionBroker);

            textViewAdapter.AddCommandFilter(filter, out var next);
            filter.Next = next;
        }
    }

    internal sealed class RazorCommandFilter : IOleCommandTarget
    {
        ICompletionSession _currentSession;

        public RazorCommandFilter(IWpfTextView textView, ICompletionBroker broker)
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

            // Is the caret in a class="" scope?
            if (IsInClassScope(out string classText) == false)
            {
                return Next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
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
                        handled = StartSession();
                        Filter();
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        handled = Complete(false);
                        break;
                    case VSConstants.VSStd2KCmdID.TAB:
                        handled = Complete(true);
                        retrigger = RetriggerIntellisense(classText);
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
                StartSession();
            }

            if (ErrorHandler.Succeeded(hresult))
            {
                if (pguidCmdGroup == VSConstants.VSStd2K)
                {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID)
                    {
                        case VSConstants.VSStd2KCmdID.TYPECHAR:
                            var character = GetTypeChar(pvaIn);
                            if (CharsAfterSignificantPoint(classText) <= 1 || character == ' ' || character == ':' || character == '/')
                            {
                                _currentSession?.Dismiss();
                                StartSession();
                            }
                            else if (_currentSession != null)
                            {
                                Filter();
                            }
                            break;
                        case VSConstants.VSStd2KCmdID.DELETEWORDLEFT:
                            _currentSession?.Dismiss();
                            StartSession();
                            break;
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                            // backspace is applied after this function is called, so this is actually
                            // equivalent to <= 1 (like above)
                            if (CharsAfterSignificantPoint(classText) <= 2 || classText.EndsWith("/"))
                            {
                                _currentSession?.Dismiss();
                                StartSession();
                            }
                            else if (_currentSession != null)
                            {
                                Filter();
                            }
                            break;
                    }
                }
            }

            return hresult;
        }

        private bool IsInClassScope(out string classText)
        {
            var startPos = new SnapshotPoint(TextView.TextSnapshot, 0);
            var caretPos = TextView.Caret.Position.BufferPosition;

            var searchSnapshot = new SnapshotSpan(startPos, caretPos);
            var text = searchSnapshot.GetText();

            var indexOfCurrentClassAttribute = text.LastIndexOf("class=\"", StringComparison.InvariantCultureIgnoreCase);
            if (indexOfCurrentClassAttribute == -1)
            {
                classText = null;
                return false;
            }
            var quotationMarkAfterLastClassAttribute = text.IndexOf('\"', indexOfCurrentClassAttribute);
            var lastQuotationMark = text.LastIndexOf('\"');

            if (lastQuotationMark == quotationMarkAfterLastClassAttribute)
            {
                classText = text.Substring(lastQuotationMark + 1);
                return true;
            }
            else
            {
                classText = null;
                return false;
            }
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
                var moveOneBack = _currentSession.SelectedCompletionSet.SelectionStatus.Completion.InsertionText.EndsWith("]");
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
        bool StartSession()
        {
            if (_currentSession != null)
                return false;

            SnapshotPoint caret = TextView.Caret.Position.BufferPosition;
            ITextSnapshot snapshot = caret.Snapshot;

            var completionActive = Broker.IsCompletionActive(TextView);

            if (completionActive)
            {
                _currentSession = Broker.GetSessions(TextView).FirstOrDefault(s => s.SelectedCompletionSet?.DisplayName.Contains("Shim") == false);
            }
            if (!completionActive || _currentSession is null)
            {
                _currentSession = Broker.CreateCompletionSession(TextView, snapshot.CreateTrackingPoint(caret, PointTrackingMode.Positive), true);
            }
            _currentSession.Dismissed += (sender, args) => _currentSession = null;

            _currentSession.Start();
            return true;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
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
    }

    #endregion
}