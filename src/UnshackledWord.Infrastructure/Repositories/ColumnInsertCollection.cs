using System.Runtime.CompilerServices;
using UnshackledWord.Domain.Extensions;

namespace UnshackledWord.Infrastructure.Repositories;

/// <summary>
/// Collection with two Lists. The first List is for column values, the second List (InsertRows) is for rows to insert.
/// List one supplies the values that are combined into rows in List two (InsertRows).
/// </summary>
public sealed class ColumnInsertCollection : List<string>
{
    public List<string> InsertRows { get; set; } = [];
    private Dictionary<string, int> _columnNames = [];
    private int _columnIndex = 1;

    public ColumnInsertCollection AddString(string? value, [CallerArgumentExpression(nameof(value))] string columnName = null!)
    {
        AddColumnName(columnName);
        if (value is null)
        {
            Add("NULL");
        }
        else
        {
            var escapedValue = value.Replace("'", "''");
            Add($"'{escapedValue}'");
        }

        return this;
    }

    public ColumnInsertCollection AddInt(int? value, [CallerArgumentExpression(nameof(value))] string columnName = null!)
    {
        AddColumnName(columnName);
        if (value is null)
        {
            Add("NULL");
        }
        else
        {
            Add(value.ToString()!);
        }

        return this;
    }

    public ColumnInsertCollection AddBool(bool value, [CallerArgumentExpression(nameof(value))] string columnName = null!)
    {
        AddColumnName(columnName);
        Add(value ? "TRUE" : "FALSE");
        return this;
    }

    public void ValuesToInsertRow()
    {
        InsertRows.Add($"({this.JoinStrings(", ")})");
    }

    public string GetColumnNames()
    {
        var orderedColumnNames = _columnNames.OrderBy(kv => kv.Value).Select(kv => kv.Key);
        return $"\"{orderedColumnNames.JoinStrings("\", \"")}\"";
    }

    public string GetAllInsertRows()
    {
        return $"{InsertRows.JoinStrings($",{Environment.NewLine}")}";
    }

    public string GetParameterNames()
    {
        var orderedColumnNames = _columnNames.OrderBy(kv => kv.Value).Select(kv => $"@{kv.Key}");
        return $"{orderedColumnNames.JoinStrings(", ")}";
    }

    private void AddColumnName(string columnName)
    {
        if (_columnNames.Keys.Count > 100)
        {
            return;
        }

        if (columnName.Contains('.'))
        {
            columnName = columnName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();
        }

        if (!_columnNames.ContainsKey(columnName))
        {
            _columnNames[columnName] = _columnIndex;
            _columnIndex++;
        }
    }
}
