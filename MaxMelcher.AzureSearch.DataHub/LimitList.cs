using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace MaxMelcher.AzureSearch.DataHub
{
    public class LimitList<T> : ObservableCollection<T>
    {
        private LinkedList<T> _list = new LinkedList<T>();
        private object _locker = new object();
        public LimitList(int maximumCount)
        {
            if (maximumCount <= 0)
                throw new ArgumentException(null, "maximumCount");

            MaximumCount = maximumCount;
        }

        public int MaximumCount { get; private set; }

        public new void Add(T value)
        {
            base.Add(value);
            lock (_locker)
            {
                if (_list.Count == MaximumCount)
                {
            
                    _list.RemoveLast();
                }
                _list.AddFirst(value);
                OnPropertyChanged(new PropertyChangedEventArgs("LimitList"));
            }
        }

    }
}