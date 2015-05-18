using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MaxMelcher.AzureSearch.DataHub
{
    public class FixedSizeObservableCollection<T> : ObservableCollection<T>
    {
        private readonly int maxSize;

        public FixedSizeObservableCollection(int maxSize)
        {
            this.maxSize = maxSize;
        }

        protected override void InsertItem(int index, T item)
        {
            CheckReentrancy();
            if (Count == maxSize)
            {
                RemoveAt(Count - 1);
            }
            base.InsertItem(0, item);
        }
    }

}