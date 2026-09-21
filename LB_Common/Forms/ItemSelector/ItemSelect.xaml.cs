using CommunityToolkit.Mvvm.Input;
using LB_Common.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace LB_Common.Forms
{
    /// <summary>
    /// Interaction logic for ItemSelector.xaml
    /// </summary>
    public partial class ItemSelector : AutoObservableWindow
    {
        public ObservableCollection<Item> Items { get; set; }
        private readonly bool _isMultiSelect = false;

        public Item SelectedItem { get; set; }
        public List<Item> SelectedItems { get; set; }

        public string BooleanParameterName { get; set; }
        public string BooleanParameterToolTip { get; set; }
        public bool BooleanParameter { get; set; }

        public int BitmapWidth { get; set; } = 92;
        public int BitmapHeight { get; set; } = 92;
        public int BitmapColumnWidth { get; set; } = 100;

        public ItemSelector(IEnumerable<Item> items, string itemName, bool multiSelect = false)
        {
            _isMultiSelect = multiSelect;
            Items = new ObservableCollection<Item>(items);
            InitializeComponent();
            DataContext = this;
            Owner = Application.Current.MainWindow;
            Title = string.Format("Select {0}", itemName);
            listBox.SelectionMode = _isMultiSelect ? DataGridSelectionMode.Extended : DataGridSelectionMode.Single;

            preNameColumn.Visibility = Items.OfType<ItemExtended>().Any(x => !string.IsNullOrWhiteSpace(x.PreNameString)) ? Visibility.Visible : Visibility.Collapsed;
            postNameColumn.Visibility = Items.OfType<ItemExtended>().Any(x => !string.IsNullOrWhiteSpace(x.PostNameString)) ? Visibility.Visible : Visibility.Collapsed;
            imageColumn.Visibility = Items.OfType<ItemExtendedImage>().Any(x => x.HasBitmap) ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetBitmapDisplaySettings(int width, int height, int columnWidth)
        {
            BitmapWidth = width;
            BitmapHeight = height;
            BitmapColumnWidth = columnWidth;
        }

        public void SetColumnNames(string id = "ID", string name = "Name", string preName = "Extra1", string postName = "Extra2")
        {
            idColumn.Header = id;
            nameColumn.Header = name;
            preNameColumn.Header = preName;
            postNameColumn.Header = postName;
        }


        [RelayCommand(CanExecute = nameof(IsItemSelected))]
        private void SelectItem()
        {
            if (_isMultiSelect)
            {
                SelectedItems = listBox.SelectedItems.Cast<Item>().ToList();
                SelectedItem = SelectedItems[0];
            }
            else
            {
                SelectedItem = listBox.SelectedItem as Item;
            }
            Close();
        }

        private bool IsItemSelected()
        {
            return listBox.SelectedItem != null;
        }

        #region Search
        private string _searchFilter = null;
        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                _searchFilter = value;
                RefreshSearchResults();
                NotifyPropertyChanged(nameof(SearchFilter));
            }
        }

        private ListCollectionView _filterList = null;
        public ListCollectionView FilterList
        {
            get
            {
                if (_filterList == null && Items != null)
                {
                    _filterList = new ListCollectionView(Items);
                    _filterList.Filter = new Predicate<object>(SearchFilterCheck);
                }
                return _filterList;
            }
            set
            {
                if (value != _filterList)
                {
                    _filterList = value;
                    NotifyPropertyChanged(nameof(FilterList));
                }
            }
        }

        public bool SearchFilterCheck(object material)
        {
            if (string.IsNullOrWhiteSpace(SearchFilter)) return true;
            var item = material as Item;
            string searchParam = SearchFilter.ToLower();

            if (item != null)
            {
                if (item.Name != null)
                {
                    if (item.Name.ToLower().Contains(searchParam)) return true;
                }

                if(item is ItemExtended extendedItem)
                {
                    if (extendedItem.PreNameString != null && extendedItem.PreNameString.ToLower().Contains(searchParam)) return true;
                    if (extendedItem.PostNameString != null && extendedItem.PostNameString.ToLower().Contains(searchParam)) return true;
                }

                int num;
                if (int.TryParse(searchParam, out num))
                {
                    if (item.ID == num) return true;
                }

            }

            return false;
        }

        private void RefreshSearchResults()
        {
            if (_filterList == null)
                _filterList = new ListCollectionView(Items);

            _filterList.Filter = new Predicate<object>(SearchFilterCheck);
            NotifyPropertyChanged(nameof(FilterList));
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchFilter = string.Empty;
        }
        #endregion

        public void SetBooleanParameter(string name, string tooltip)
        {
            BooleanParameterName = name;
            BooleanParameterToolTip = tooltip;
            NotifyPropertyChanged(nameof(BooleanParameterName));
            NotifyPropertyChanged(nameof(BooleanParameterToolTip));
            checkbox.Visibility = Visibility.Visible;
        }
    }
}
