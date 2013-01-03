using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Data;
using SharedClasses;
using Microsoft.Win32;
using System.Windows.Interop;
using System.IO;
using ExtendedGrid.ExtendedGridControl;

namespace CompareCSVs
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public class MyData
		{
			public string name { set; get; }
			public string artist { set; get; }
			public string location { set; get; }
		}

		public MainWindow()
		{
			InitializeComponent();

			AddComboboxItems(comboboxCSV1HeaderRowCount);
			AddComboboxItems(comboboxCSV2HeaderRowCount);

			this.Width = 1000;
			this.Height = 600;
		}

		private void AddComboboxItems(ComboBox combo)
		{
			combo.Items.Clear();
			combo.Items.Add(0);
			combo.Items.Add(1);
			combo.Items.Add(2);
			combo.SelectedIndex = 0;
		}

		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
		}

		private void buttonLoadCSV1(object sender, RoutedEventArgs e)
		{
			string filepath = SelectFile("Please select file for CSV 1", null, "CSV Files (*.csv)|*.csv");
			if (filepath == null) return;
			DataTable table = CSVReader.ReadCSVFile(filepath, false, 0);
			dataGridCSV1.ItemsSource = table.DefaultView;
			dataGridCSV1.AutoGenerateColumns = true;
		}

		private void buttonLoadCSV2(object sender, RoutedEventArgs e)
		{
			string filepath = SelectFile("Please select file for CSV 2", null, "CSV Files (*.csv)|*.csv");
			if (filepath == null) return;
			DataTable table = CSVReader.ReadCSVFile(filepath, false, 0);
			dataGridCSV2.ItemsSource = table.DefaultView;
			dataGridCSV2.AutoGenerateColumns = true;
		}

		private void buttonCompareNow(object sender, RoutedEventArgs e)
		{
			CompareNow();
		}

		private void CompareNow()
		{
			int csv1headerRowcount = (int)comboboxCSV1HeaderRowCount.SelectedItem;
			int csv2headerRowcount = (int)comboboxCSV2HeaderRowCount.SelectedItem;

			if (dataGridCSV1.Columns.Count == 0 || dataGridCSV2.Columns.Count == 0)
				UserMessages.ShowWarningMessage("Please make sure both tables have data, column count > 0");
			else if (dataGridCSV1.Columns.Count != dataGridCSV2.Columns.Count)
				UserMessages.ShowWarningMessage("Please make sure both tables have same number of columns");
			else if (dataGridCSV1.Items.Count == 0 || dataGridCSV2.Items.Count == 0)
				UserMessages.ShowWarningMessage("Please make sure both tables have data, row count > 0");
			//else if (dataGridCSV1.Items.Count - csv1headerRowcount != dataGridCSV2.Items.Count - csv2headerRowcount)
			//    UserMessages.ShowWarningMessage("Please make sure both tables have same number of rows, is the setting for [Number Of Header Rows] correct?.");
			else//Same number of cells, both tables populated, can now do comparison
			{
				int colcount = dataGridCSV1.Columns.Count;
				int rowcount1 = dataGridCSV1.Items.Count - csv1headerRowcount - (dataGridCSV1.CanUserAddRows ? 1 : 0);
				int rowcount2 = dataGridCSV2.Items.Count - csv2headerRowcount - (dataGridCSV2.CanUserAddRows ? 1 : 0);
				int minrowcount = Math.Min(rowcount1, rowcount2);//Because the rowcount might be different, it will compare up to the last row of table with least rows
				int maxrowcount = Math.Max(rowcount1, rowcount2);
				int gridwithleastrows = rowcount1 == minrowcount ? 1 : 2;//The grid number which has the least rows (this variable will not be used if row counts are the same for both grids)

				DataTable comparisonTable = GenerateBlankTable(maxrowcount, colcount);

				//int rowcount = dataGridCSV1.Items.Count - csv1headerRowcount - (dataGridCSV1.CanUserAddRows ? 1 : 0);
				for (int row = 0; row < maxrowcount; row++)
				{
					int grid1rownum = row + csv1headerRowcount;
					int grid2rownum = row + csv2headerRowcount;

					if (row >= minrowcount)
					{
						for (int col = 0; col < dataGridCSV1.Columns.Count; col++)
							comparisonTable.Rows[row][col] = "MissingFrom" + gridwithleastrows;
					}
					else
					{
						DataRowView rowviewCSV1 = dataGridCSV1.Items[grid1rownum] as DataRowView;
						if (rowviewCSV1 == null)
						{
							UserMessages.ShowWarningMessage("Unable to get DataRowView from type (CSV 1): " + dataGridCSV1.Items[grid1rownum].GetType());
							return;
						}
						DataRowView rowviewCSV2 = dataGridCSV2.Items[grid2rownum] as DataRowView;
						if (rowviewCSV2 == null)
						{
							UserMessages.ShowWarningMessage("Unable to get DataRowView from type (CSV 2): " + dataGridCSV2.Items[grid2rownum].GetType());
							return;
						}
						var cellsCSV1 = rowviewCSV1.Row.ItemArray;
						var cellsCSV2 = rowviewCSV2.Row.ItemArray;

						for (int col = 0; col < dataGridCSV1.Columns.Count; col++)
						{
							var cellCSV1 = (cellsCSV1[col] ?? "").ToString();
							var cellCSV2 = (cellsCSV2[col] ?? "").ToString();

							string cellCompareResult = "";// "E";
							if (!cellCSV1.Equals(cellCSV2))
							{
								cellCompareResult = "Inequal";
								if (cellCSV1.Equals(cellCSV2, StringComparison.InvariantCultureIgnoreCase))
									cellCompareResult = "Equal_CasingDiff";
								double tmpdouble1;//Try to see whether its a double and compare the "double" value
								double tmpdouble2;
								if (double.TryParse(cellCSV1, out tmpdouble1) && double.TryParse(cellCSV2, out tmpdouble2))
								{
									if (tmpdouble1 == tmpdouble2)
										cellCompareResult = "Equal_DoubleValue";
									else if (Math.Round(tmpdouble1) == Math.Round(tmpdouble2))
										cellCompareResult = "Equal_RoundedInt";
								}
							}
							comparisonTable.Rows[row][col] = cellCompareResult;
						}
					}
				}

				dataGridComparisonResults.ItemsSource = comparisonTable.DefaultView;
				dataGridComparisonResults.AutoGenerateColumns = true;
			}
			dataGridComparisonResults.UpdateLayout();
			ResizeAllColumns();
		}

		public static string SelectFile(string title, string initialDir = null, string filterstring = null, Window owner = null)
		{
			OpenFileDialog ofd = new OpenFileDialog();
			ofd.Multiselect = false;
			ofd.CheckFileExists = true;

			ofd.Title = title;
			if (filterstring != null)
				ofd.Filter = filterstring;
			if (initialDir != null)
				ofd.InitialDirectory = initialDir;
			bool? showresult =
				owner != null ? ofd.ShowDialog(owner) : ofd.ShowDialog();
			if (showresult == true)
				return ofd.FileName;
			else
				return null;
		}

		public static string SelectSavetoFile(string title, string initialDir = null, string filterstring = null, Window owner = null)
		{
			SaveFileDialog sfd = new SaveFileDialog();

			sfd.Title = title;
			if (filterstring != null)
				sfd.Filter = filterstring;
			if (initialDir != null)
				sfd.InitialDirectory = initialDir;
			bool? showresult =
				owner != null ? sfd.ShowDialog(owner) : sfd.ShowDialog();
			if (showresult == true)
				return sfd.FileName;
			else
				return null;
		}

		private void menuitemPasteFromClipboardCSV1(object sender, RoutedEventArgs e)
		{
			var clipboarddata = ClipboardHelper.ParseClipboardData();
			DataTable table = GetTableFromStrings(clipboarddata);
			dataGridCSV1.ItemsSource = table.DefaultView;
			dataGridCSV1.UpdateLayout();
			ResizeAllColumns();
			dataGridComparisonResults.ItemsSource = null;
		}

		private void menuitemPasteFromClipboardCSV2(object sender, RoutedEventArgs e)
		{
			var clipboarddata = ClipboardHelper.ParseClipboardData();
			DataTable table = GetTableFromStrings(clipboarddata);
			dataGridCSV2.ItemsSource = table.DefaultView;
			dataGridCSV2.UpdateLayout();
			ResizeAllColumns();
			dataGridComparisonResults.ItemsSource = null;
		}

		private void menuitemCompareNow(object sender, RoutedEventArgs e)
		{
			CompareNow();
		}

		private void SetFixedRows()
		{
		}

		private void ResizeAllColumns()
		{
			if (dataGridCSV1 == null) return;//Happens on startup because value changed of 'slider1'

			int colcount = dataGridCSV1.Columns.Count;
			for (int i = 0; i < colcount; i++)
			{
				double maxwidth = 0;
				if (dataGridCSV1.Columns[i].ActualWidth > maxwidth) maxwidth = dataGridCSV1.Columns[i].ActualWidth;
				if (dataGridCSV2.Columns.Count == colcount)
					if (dataGridCSV2.Columns[i].ActualWidth > maxwidth) maxwidth = dataGridCSV2.Columns[i].ActualWidth;
				if (dataGridComparisonResults.Columns.Count == colcount)
					if (dataGridComparisonResults.Columns[i].ActualWidth > maxwidth) maxwidth = dataGridComparisonResults.Columns[i].ActualWidth;

				if (dataGridCSV1.Columns[i].Width != maxwidth)
					dataGridCSV1.Columns[i].Width = maxwidth;
				if (dataGridCSV2.Columns.Count == colcount)
					if (dataGridCSV2.Columns[i].Width != maxwidth)
						dataGridCSV2.Columns[i].Width = maxwidth;
				if (dataGridComparisonResults.Columns.Count == colcount)
					if (dataGridComparisonResults.Columns[i].Width != maxwidth)
						dataGridComparisonResults.Columns[i].Width = maxwidth;
			}
		}

		private DataTable GetTableFromStrings(List<string[]> rowData)
		{
			DataTable table = new DataTable();
			int colcount = rowData.Max(arr => arr.Length);
			for (int i = 0; i < colcount; i++)
			{
				table.Columns.Add();
				table.Columns[i].DataType = typeof(string);
				table.Columns[i].ColumnName = "Column" + (i + 1);
			}

			foreach (string[] rowarray in rowData)
			{
				table.Rows.Add(rowarray);
				//var datarow = table.NewRow();
				//datarow.ItemArray = rowarray;
				//table.Rows.Add(datarow);
			}
			return table;
		}

		private DataTable GenerateBlankTable(int numRows, int numCols)
		{
			DataTable table = new DataTable();
			for (int i = 0; i < numCols; i++)
			{
				table.Columns.Add();
				table.Columns[i].DataType = typeof(string);
				table.Columns[i].ColumnName = "Column" + (i + 1);
			}
			string[] blankCellsArray = new string[numCols];
			for (int i = 0; i < numRows; i++)
				table.Rows.Add(blankCellsArray);
			return table;
		}

		private void dataGridComparisonResults_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			return;
			if (dataGridComparisonResults.SelectedCells.Count == 0)
			{
				UserMessages.ShowWarningMessage("Please select cells first before double-clicking");
				return;
			}
			if (dataGridComparisonResults.SelectedCells.Count > 1)
			{
				UserMessages.ShowWarningMessage("Can only double-click with one selected cell");
				return;
			}
			var cell = dataGridComparisonResults.SelectedCells[0];
			var rownum = dataGridComparisonResults.Items.IndexOf(cell.Item);
			var colnum = dataGridComparisonResults.Columns.IndexOf(cell.Column);
			//tabItemCSV2.IsSelected = true;
			//tabItemCSV2.Focus();
			//tabControl1.UpdateLayout();
			//dataGridCSV2.Focus();
			//DataRowView rowviewCSV2 = dataGridCSV2.Items[rownum] as DataRowView;
			//if (rowviewCSV2 == null)
			//{
			//    UserMessages.ShowWarningMessage("Unable to get DataRowView from type (CSV 2): " + dataGridCSV1.Items[rownum].GetType());
			//    return;
			//}
			//var cellsCSV2 = rowviewCSV2.Row.ItemArray;
			//dataGridCSV2.SelectedIndex = rownum;
			//var cell2 = cellsCSV2[colnum];
			//DataRowView rowview = dataGridCSV2.Items[rownum] as DataRowView;
			dataGridCSV1.Focus();
			dataGridCSV1.CurrentCell = new DataGridCellInfo(dataGridCSV1.Items[rownum], dataGridCSV1.Columns[colnum]);
			dataGridCSV2.SelectedCells.Clear();
			dataGridCSV2.SelectedCells.Add(dataGridCSV2.CurrentCell);
			dataGridCSV2.Focus();
			dataGridCSV2.CurrentCell = new DataGridCellInfo(dataGridCSV2.Items[rownum], dataGridCSV2.Columns[colnum]);
			dataGridCSV2.SelectedCells.Clear();
			dataGridCSV2.SelectedCells.Add(dataGridCSV2.CurrentCell);
			//dataGridCSV2.CurrentCell = dataGridCSV2.Items[0];
			//dataGridCSV2.CurrentColumn = dataGridCSV2.Columns[colnum];
			//dataGridCSV2.SelectedItem = cellsCSV2[colnum];
		}

		private static void TransferSelectionToCSVgrids(ExtendedDataGrid sourceGrid, Dictionary<ExtendedDataGrid, int> gridsAndHeaderRowCount, params ExtendedDataGrid[] destinationGrids)
		{
			if (sourceGrid.SelectedCells.Count == 0) return;

			foreach (var destgrid in destinationGrids)
				destgrid.SelectedCells.Clear();
			var selectedcells = sourceGrid.SelectedCells;
			foreach (var selcell in selectedcells)
			{
				var sourcerownum = sourceGrid.Items.IndexOf(selcell.Item);
				var sourcecolnum = sourceGrid.Columns.IndexOf(selcell.Column);

				if (gridsAndHeaderRowCount.ContainsKey(sourceGrid))
					sourcerownum = sourcerownum - gridsAndHeaderRowCount[sourceGrid];
				if (sourcerownum < 0)
					continue;//Wont be able to transfer selection, just skip for this cell

				foreach (var destgrid in destinationGrids)
				{
					int destinationrownum = sourcerownum;
					if (gridsAndHeaderRowCount.ContainsKey(destgrid))
						destinationrownum = sourcerownum + gridsAndHeaderRowCount[destgrid];

					if (destinationrownum >= destgrid.Items.Count - (destgrid.CanUserAddRows ? 1 : 0)
						|| sourcecolnum >= destgrid.Columns.Count)
						continue;//Means the cell we try to select is out of bounds

					var cellinfo = new DataGridCellInfo(destgrid.Items[destinationrownum], destgrid.Columns[sourcecolnum]);
					destgrid.SelectedCells.Add(cellinfo);
					destgrid.ScrollIntoView(cellinfo.Item, cellinfo.Column);
				}
			}
		}

		private bool pauseCSV1selectionChangedEvents = false;
		private void dataGridCSV1_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
		{
			if (pauseCSV1selectionChangedEvents) return;

			pauseCSV2selectionChangedEvents = true;
			pauseComparisonselectionChangedEvents = true;
			try
			{
				int csv1headerRowcount = (int)comboboxCSV1HeaderRowCount.SelectedItem;
				int csv2headerRowcount = (int)comboboxCSV2HeaderRowCount.SelectedItem;
				var gridsWithHeaderRowCount = new Dictionary<ExtendedDataGrid,int>();
				gridsWithHeaderRowCount.Add(dataGridCSV1, csv1headerRowcount);
				gridsWithHeaderRowCount.Add(dataGridCSV2, csv2headerRowcount);

				TransferSelectionToCSVgrids(dataGridCSV1, gridsWithHeaderRowCount, dataGridCSV2, dataGridComparisonResults);
			}
			finally
			{
				pauseCSV2selectionChangedEvents = false;
				pauseComparisonselectionChangedEvents = false;
			}
		}

		private bool pauseCSV2selectionChangedEvents = false;
		private void dataGridCSV2_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
		{
			if (pauseCSV2selectionChangedEvents) return;

			pauseCSV1selectionChangedEvents = true;
			pauseComparisonselectionChangedEvents = true;
			try
			{
				int csv1headerRowcount = (int)comboboxCSV1HeaderRowCount.SelectedItem;
				int csv2headerRowcount = (int)comboboxCSV2HeaderRowCount.SelectedItem;
				var gridsWithHeaderRowCount = new Dictionary<ExtendedDataGrid, int>();
				gridsWithHeaderRowCount.Add(dataGridCSV1, csv1headerRowcount);
				gridsWithHeaderRowCount.Add(dataGridCSV2, csv2headerRowcount);

				TransferSelectionToCSVgrids(dataGridCSV2, gridsWithHeaderRowCount, dataGridCSV1, dataGridComparisonResults);
			}
			finally
			{
				pauseCSV1selectionChangedEvents = false;
				pauseComparisonselectionChangedEvents = false;
			}
		}

		private bool pauseComparisonselectionChangedEvents = false;
		private void dataGridComparisonResults_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
		{
			if (pauseComparisonselectionChangedEvents) return;
			
			pauseCSV1selectionChangedEvents = true;
			pauseCSV2selectionChangedEvents = true;
			try
			{
				int csv1headerRowcount = (int)comboboxCSV1HeaderRowCount.SelectedItem;
				int csv2headerRowcount = (int)comboboxCSV2HeaderRowCount.SelectedItem;
				var gridsWithHeaderRowCount = new Dictionary<ExtendedDataGrid, int>();
				gridsWithHeaderRowCount.Add(dataGridCSV1, csv1headerRowcount);
				gridsWithHeaderRowCount.Add(dataGridCSV2, csv2headerRowcount);

				TransferSelectionToCSVgrids(dataGridComparisonResults, gridsWithHeaderRowCount, dataGridCSV1, dataGridCSV2);
			}
			finally
			{
				pauseCSV1selectionChangedEvents = false;
				pauseCSV2selectionChangedEvents = false;
			}
			//Console.WriteLine("Selected: " + string.Join(",", e.AddedCells.Select(cell => cell.Column.DisplayIndex)));
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			if (gridForDataGrids.RowDefinitions.Count > 0)
			{
				gridForDataGrids.RowDefinitions.Clear();
				gridForDataGrids.ColumnDefinitions.Clear();
				gridForDataGrids.ColumnDefinitions.Add(new ColumnDefinition());
				gridForDataGrids.ColumnDefinitions.Add(new ColumnDefinition());
				gridForDataGrids.ColumnDefinitions.Add(new ColumnDefinition());
			}
			else
			{
				gridForDataGrids.RowDefinitions.Clear();
				gridForDataGrids.ColumnDefinitions.Clear();
				gridForDataGrids.RowDefinitions.Add(new RowDefinition());
				gridForDataGrids.RowDefinitions.Add(new RowDefinition());
				gridForDataGrids.RowDefinitions.Add(new RowDefinition());
			}
		}

		private void aboutLabel_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			AboutWindow2.ShowAboutWindow(new System.Collections.ObjectModel.ObservableCollection<DisplayItem>()
			{ 
				new DisplayItem("Author", "Francois Hill"),
				new DisplayItem("Icon(s) obtained from", "http://www.iconfinder.com", "http://www.iconfinder.com/icondetails/7564/128/diagram_graph_log_rating_scale_icon")
			},
			true);
		}

		private ExtendedGrid.Classes.ExcelTableStyle? PickTableStyle()
		{
			var stylelist = new List<ExtendedGrid.Classes.ExcelTableStyle>();
			foreach (var style in Enum.GetValues(typeof(ExtendedGrid.Classes.ExcelTableStyle)))
				stylelist.Add((ExtendedGrid.Classes.ExcelTableStyle)style);
			var tablestyle = PickItemWPF.PickItem(typeof(ExtendedGrid.Classes.ExcelTableStyle), Enum.GetValues(typeof(ExtendedGrid.Classes.ExcelTableStyle)), "Please choose the style for the excel sheet (cancel to use no style)", null);
			if (tablestyle != null)
				return (ExtendedGrid.Classes.ExcelTableStyle)tablestyle;
			else
				return null;
		}

		private bool exportBusy = false;

		private void SaveDataGridToExcel(ExtendedDataGrid grid)
		{
			if (exportBusy)
				UserMessages.ShowWarningMessage("Please wait for export to finish...");
			exportBusy = true;

			var excelfilePath = SelectSavetoFile("Select Excel file to save the data as", null, "Excel (2007-2010) Files (*.xlsx)|*.xlsx");
			if (excelfilePath == null) return;
			ExtendedGrid.Classes.ExcelTableStyle? tableStyle = PickTableStyle();
			ThreadingInterop.DoAction(() =>
			{
				try
				{
					this.Dispatcher.Invoke((Action)delegate
					{
						var curselectionunit = grid.SelectionUnit;
						grid.SelectionUnit = DataGridSelectionUnit.FullRow;
						if (tableStyle.HasValue)
							grid.ExportToExcel("Sheet", excelfilePath, tableStyle.Value, true);
						else
							grid.ExportToExcel("Sheet", excelfilePath, true);
						grid.SelectionUnit = curselectionunit;
					});
				}
				catch (Exception exc)
				{
					UserMessages.ShowErrorMessage("Error while exporting: " + exc.Message);
				}
				finally
				{
					exportBusy = false;
				}
			},
			false);
		}

		private void SaveDataGridToCSV(ExtendedDataGrid grid)
		{
			if (exportBusy)
				UserMessages.ShowWarningMessage("Please wait for export to finish...");
			exportBusy = true;

			ThreadingInterop.DoAction(() =>
			{
				try
				{
					this.Dispatcher.Invoke((Action)delegate
					{
						var curselectionunit = grid.SelectionUnit;
						grid.SelectionUnit = DataGridSelectionUnit.FullRow;
						var csvpath = SelectSavetoFile("Select CSV file to save the data as", null, "CSV Files (*.csv)|*.csv");
						if (csvpath == null) return;
						grid.ExportToCsv("CSVsheet", csvpath, true);
						grid.SelectionUnit = curselectionunit;
					});
				}
				catch (Exception exc)
				{
					UserMessages.ShowErrorMessage("Error while exporting: " + exc.Message);
				}
				finally
				{
					exportBusy = false;
				}
			},
			false);
		}

		private void SaveDataGridToPDF(ExtendedDataGrid grid)
		{
			if (exportBusy)
				UserMessages.ShowWarningMessage("Please wait for export to finish...");
			exportBusy = true;

			ThreadingInterop.DoAction(() =>
			{
				try
				{
					this.Dispatcher.BeginInvoke((Action)delegate
					{
						var curselectionunit = grid.SelectionUnit;
						grid.SelectionUnit = DataGridSelectionUnit.FullRow;
						var pdfpath = SelectSavetoFile("Select PDF file to save the data as", null, "PDF Files (*.pdf)|*.pdf");
						if (pdfpath == null) return;
						ExtendedGrid.Classes.ExcelTableStyle? tableStyle = PickTableStyle();
						if (tableStyle.HasValue)
							grid.ExportToPdf("PDFsheet1", pdfpath, tableStyle.Value, true);
						else
							grid.ExportToPdf("PDFsheet1", pdfpath, true);
						grid.SelectionUnit = curselectionunit;
					});
				}
				catch (Exception exc)
				{
					UserMessages.ShowErrorMessage("Error while exporting: " + exc.Message);
				}
				finally
				{
					exportBusy = false;
				}
			},
			false);
		}

		private void menuitemSaveToExcelFileCSV1(object sender, RoutedEventArgs e)
		{
			SaveDataGridToExcel(dataGridCSV1);
		}

		private void menuitemSaveToCSVFileCSV1(object sender, RoutedEventArgs e)
		{
			SaveDataGridToCSV(dataGridCSV1);
		}

		private void menuitemSaveToPDFFileCSV1(object sender, RoutedEventArgs e)
		{
			SaveDataGridToPDF(dataGridCSV1);
		}

		private void menuitemSaveToExcelFileCSV2(object sender, RoutedEventArgs e)
		{
			SaveDataGridToExcel(dataGridCSV2);
		}

		private void menuitemSaveToCSVFileCSV2(object sender, RoutedEventArgs e)
		{
			SaveDataGridToCSV(dataGridCSV2);
		}

		private void menuitemSaveToPDFFileCSV2(object sender, RoutedEventArgs e)
		{
			SaveDataGridToPDF(dataGridCSV2);
		}

		private void slider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (dataGridCSV1 == null) return;

			dataGridCSV1.FontSize = slider1.Value;
			dataGridCSV2.FontSize = slider1.Value;
			dataGridComparisonResults.FontSize = slider1.Value;

			var is1 = dataGridCSV1.ItemsSource;
			dataGridCSV1.ItemsSource = null;
			dataGridCSV1.ItemsSource = is1;

			var is2 = dataGridCSV2.ItemsSource;
			dataGridCSV2.ItemsSource = null;
			dataGridCSV2.ItemsSource = is2;

			var isc = dataGridComparisonResults.ItemsSource;
			dataGridComparisonResults.ItemsSource = null;
			dataGridComparisonResults.ItemsSource = isc;

			dataGridCSV1.UpdateLayout();
			dataGridCSV1.InvalidateVisual();
			dataGridCSV2.InvalidateVisual();
			dataGridCSV2.UpdateLayout();
			dataGridComparisonResults.UpdateLayout();
			dataGridComparisonResults.InvalidateVisual();
			ResizeAllColumns();
		}
	}

	//public class ComparisonCellBackgroundConverter : IValueConverter
	//{
	//    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
	//    {
	//        var val = value;
	//        //return null;
	//        return "Equal";
	//    }

	//    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
	//    {
	//        throw new NotImplementedException();
	//    }
	//}
}
