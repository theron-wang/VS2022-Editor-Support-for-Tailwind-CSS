using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TailwindCSSIntellisense.Completions
{
    internal class BulkInsertObservableCollection<T> : BulkObservableCollection<T>
    {
        private readonly Dispatcher _dispatcher;
        private const string _collectionChangedDuringRangeOperation = "_collectionChangedDuringRangeOperation";
        private delegate void AddRangeToBeginningCallback(IList<T> items);

        public BulkInsertObservableCollection()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// Functionally equivalent to <see cref="List{T}.InsertRange(int, IEnumerable{T})"/> where the first parameter is 0
        /// </summary>
        /// <param name="items">The list of items to prepend</param>
        public void AddRangeToBeginning(IEnumerable<T> items)
        {
            if (items == null || !items.Any())
            {
                return;
            }

            if (_dispatcher.CheckAccess())
            {
                try
                {
                    BeginBulkOperation();
                    SetField(_collectionChangedDuringRangeOperation, true);
                    foreach (T item in items.Reverse())
                    {
                        Items.Insert(0, item);
                    }
                }
                finally
                {
                    EndBulkOperation();
                }
            }
            else
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await _dispatcher.BeginInvoke(DispatcherPriority.Send, new AddRangeToBeginningCallback(AddRangeToBeginning), items);
                });
            }
        }

        // https://stackoverflow.com/questions/6961781/reflecting-a-private-field-from-a-base-class
        private void SetField(string fieldName, object value)
        {
            Type t = GetType();
            FieldInfo fi = null;

            while (t != null)
            {
                fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

                if (fi != null) break;

                t = t.BaseType;
            }

            fi.SetValue(this, value);
        }
    }
}
