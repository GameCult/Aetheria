using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.EventSystems;
using Random = UnityEngine.Random;

public class Spreadsheet : MonoBehaviour
{
    public Prototype ColumnDividerPrototype;
    public Prototype ColumnHeaderPrototype;
    public Prototype RowPrototype;
    public Color OddRowBackground;
    public Color EvenRowBackground;
    public int MinimumColumnSize = 50;
    
    private List<RectTransform> _columnDividerInstances = new List<RectTransform>();
    private List<SpreadsheetRow> _rowInstances = new List<SpreadsheetRow>();
    private List<SpreadsheetColumnHeader> _columnInstances = new List<SpreadsheetColumnHeader>();
    private List<SpreadsheetEntryRow> _data = new List<SpreadsheetEntryRow>();
    private int[] _columnSizes;
    private int _sortColumn = -1;
    private bool _sortAscending = false;
    private Vector2 _dragStartPosition;
    private int _dragStartColumnSize;
    private int _dragStartNextColumnSize;

    public void ShowData(string[] columnNames, int[] columnSizePriorities, IEnumerable<SpreadsheetEntryRow> data)
    {
        foreach(var col in _columnInstances)
        {
            col.Button.onClick.RemoveAllListeners();
            col.GetComponent<Prototype>().ReturnToPool();
        }
        _columnInstances.Clear();
        
        foreach(var row in _rowInstances)
            row.GetComponent<Prototype>().ReturnToPool();
        _rowInstances.Clear();
        
        foreach(var div in _columnDividerInstances)
            div.GetComponent<Prototype>().ReturnToPool();
        _columnDividerInstances.Clear();
        
        _data.Clear();
        _data.AddRange(data);

        _sortColumn = -1;
        
        if(columnNames.Length != columnSizePriorities.Length)
            throw new ArgumentException("Spreadsheet column names and sizes do not match!");
        
        foreach(var d in _data)
            if(columnNames.Length!=d.Columns.Length)
                throw new ArgumentException("Spreadsheet column names and row data do not match!");

        var totalPriority = columnSizePriorities.Sum();
        var totalSize = Mathf.RoundToInt(GetComponent<RectTransform>().rect.width);
        _columnSizes = columnSizePriorities.Select(p => (int)((float) p / totalPriority * totalSize)).ToArray();

        for (var i = 0; i < columnNames.Length; i++)
        {
            var columnHeader = ColumnHeaderPrototype.Instantiate<SpreadsheetColumnHeader>();
            columnHeader.SortIcon.gameObject.SetActive(false);
            columnHeader.Title.text = columnNames[i];
            var columnIndex = i;
            columnHeader.Button.onClick.AddListener(() =>
            {
                if (_sortColumn != -1)
                {
                    _columnInstances[_sortColumn].SortIcon.gameObject.SetActive(false);
                }
                _sortAscending = _sortColumn == columnIndex && !_sortAscending;
                if(_sortAscending)
                    _data.Sort((row1, row2) => row2.Columns[columnIndex].SortKey.CompareTo(row1.Columns[columnIndex].SortKey));
                else _data.Sort((row1, row2) => row1.Columns[columnIndex].SortKey.CompareTo(row2.Columns[columnIndex].SortKey));
                _sortColumn = columnIndex;
                _columnInstances[_sortColumn].SortIcon.gameObject.SetActive(true);
                _columnInstances[_sortColumn].SortIcon.rectTransform.rotation = Quaternion.Euler(0,0, _sortAscending ? -90 : 90);
                RefreshData();
                RepositionColumns();
            });
            columnHeader.ResizeBeginDragTrigger.OnBeginDragAsObservable().Subscribe(e =>
            {
                _dragStartPosition = e.position;
                _dragStartColumnSize = _columnSizes[columnIndex];
                _dragStartNextColumnSize = columnIndex != columnNames.Length-1 ? _columnSizes[columnIndex + 1] : 0;
            });
            columnHeader.ResizeDragTrigger.OnDragAsObservable().Subscribe(e =>
            {
                var delta = e.position.x - _dragStartPosition.x;
                if (_dragStartColumnSize + (int) delta < MinimumColumnSize ||
                    columnIndex != columnNames.Length - 1 && _dragStartNextColumnSize - (int) delta < MinimumColumnSize)
                    return;
                _columnSizes[columnIndex] = _dragStartColumnSize + (int) delta;
                if(columnIndex != columnNames.Length-1)
                    _columnSizes[columnIndex+1] = _dragStartNextColumnSize - (int) delta;
                RepositionColumns();
            });
            _columnInstances.Add(columnHeader);
            if (i != columnNames.Length-1)
            {
                var columnDivider = ColumnDividerPrototype.Instantiate<RectTransform>();
                _columnDividerInstances.Add(columnDivider);
            }
        }

        for (var i = 0; i < _data.Count; i++)
        {
            var rowInstance = RowPrototype.Instantiate<SpreadsheetRow>();
            rowInstance.Background.color = i % 2 == 0 ? EvenRowBackground : OddRowBackground;
            _rowInstances.Add(rowInstance);
        }
        
        RefreshData();

        RepositionColumns();
    }

    private void RefreshData()
    {
        for (var i = 0; i < _data.Count; i++)
        {
            var rowIndex = i;
            _rowInstances[i].ShowData(_data[i]);
            _rowInstances[i].ClickTrigger.Reset();
            _rowInstances[i].ClickTrigger.OnPointerClickAsObservable().Subscribe(e =>
            {
                if (e.button == PointerEventData.InputButton.Right)
                    _data[rowIndex].OnRightClick?.Invoke();
                else
                {
                    if (e.clickCount == 2)
                    {
                        _data[rowIndex].OnDoubleClick?.Invoke();
                    }
                    else _data[rowIndex].OnClick?.Invoke();
                }
            });
        }
    }

    private void RepositionColumns()
    {
        var distance = 0;
        for (var i = 0; i < _columnSizes.Length; i++)
        {
            _columnInstances[i].Rect.anchoredPosition = Vector2.right * distance;
            _columnInstances[i].Rect.sizeDelta = Vector2.right * _columnSizes[i];
            distance += _columnSizes[i];
            if (i != _columnSizes.Length-1)
                _columnDividerInstances[i].anchoredPosition = Vector2.right * distance;
        }
        foreach(var row in _rowInstances)
            row.ApplyColumnSizes(_columnSizes);
    }
    
    void Update()
    {
        
    }
}

public class SpreadsheetEntryRow
{
    public SpreadsheetEntryColumn[] Columns;
    public Action OnClick;
    public Action OnDoubleClick;
    public Action OnRightClick;
}

public class SpreadsheetEntryColumn
{
    public IComparable SortKey;
    public Func<string> Output;
}
