using System;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;

using WPSort.de.boxberry.mgrc;
using Spire.PdfViewer.Forms;
using System.Data;
using System.Linq;

namespace WPSort
{

    struct DocRec
    {
        public string Number, id;

        public DocRec(string str, string i)
        {
            Number = str;
            id = i;
        }

    }

    public partial class Form1 : Form
    {

        MgrcSoap soapClient = new MgrcSoap();
        PdfViewer pdf = new PdfViewer();
        private string token;
        private bool PrinterSettingChange = false;
        private int PrintCounter = 0;
        DBLogs dbLog = null;
        DataSet ds;
        DataView dv;

        private int curPosition = 0, maxRead = 20;

        DocRec curDoc = new DocRec("", "0");

        public Form1()
        {

            try
            {
                dbLog = new DBLogs(Properties.Settings.Default.ConnectionString);
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            if (Properties.Settings.Default.Language != "")
            {
                try
                {
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(Properties.Settings.Default.Language);
                }
                catch (CultureNotFoundException err)
                {
                    MessageBox.Show(err.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (Properties.Settings.Default.MaxRead > 0)
            {
                maxRead = Properties.Settings.Default.MaxRead;
            }

            pdf.SetViewerMode(Spire.PdfViewer.Forms.PdfViewerMode.PdfViewerMode.SinglePage);

            soapClient.parcelsFromTransportDocumentCompleted += new parcelsFromTransportDocumentCompletedEventHandler(LoadDataDoc);

            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void changeUserToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Authorization();
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            if (dbLog == null)
                Close();

            Authorization();

            pageSetupDialog1.PageSettings = new System.Drawing.Printing.PageSettings();

            Type typePData = typeof(parcelsData);

            ds = new DataSet();
            ds.Tables.Add("main");

            foreach (var item in typePData.GetProperties())
            {
                if (item.PropertyType.Name.IndexOf("Nullable") != -1)
                {
                    if (item.PropertyType.FullName.IndexOf("Boolean") != -1)
                        ds.Tables["main"].Columns.Add(item.Name, Type.GetType("System.Boolean"));
                    else if (item.PropertyType.FullName.IndexOf("Int") != -1)
                        ds.Tables["main"].Columns.Add(item.Name, Type.GetType("System.Int32"));
                    else
                        ds.Tables["main"].Columns.Add(item.Name);

                    ds.Tables["main"].Columns[item.Name].AllowDBNull = true;
                }
                else
                    ds.Tables["main"].Columns.Add(item.Name, item.PropertyType);
            }

            ds.Tables["main"].Columns.Add("PreScaned", Type.GetType("System.Boolean"));

            dataGridView1.AutoGenerateColumns = false;

            dv = new DataView(ds.Tables["main"]);

            dataGridView1.DataSource = dv;

            dataGridView1.Columns["Scaned"].DataPropertyName = "Scaned";
            dataGridView1.Columns["Barcode"].DataPropertyName = "Barcode";
            dataGridView1.Columns["Country"].DataPropertyName = "Country";
            dataGridView1.Columns["PreScaned"].DataPropertyName = "PreScaned";

        }

        private void Authorization()
        {
            PasswordForm passwordF = new PasswordForm();
            if (passwordF.ShowDialog() == DialogResult.OK)
            {
                token = passwordF.Token.Trim();
            }
        }

        private void russianToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Language = "ru-RU";
            Properties.Settings.Default.Save();
            MessageBox.Show("Для смены языка перезапустите приложение");
        }

        private void englishToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Language = "en-US";
            Properties.Settings.Default.Save();
            MessageBox.Show("Restart application to change language");
        }

        private void printerSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (printDialog1.ShowDialog() == DialogResult.OK)
                PrinterSettingChange = true;
        }


        private void ReadDocument()
        {
            parcelsFromTransportDocumentRequest param = new parcelsFromTransportDocumentRequest();
            param.token = token;
            param.TransportNumber = curDoc.Number;
            param.limitStart = Convert.ToString(curPosition);
            param.limitCount = Convert.ToString(maxRead);

            try
            {
                soapClient.parcelsFromTransportDocumentAsync(param);
                button3.Enabled = false;
                BarcodeBox.ReadOnly = true;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void LoadDataDoc(object sender, parcelsFromTransportDocumentCompletedEventArgs e)
        {
            int countBarcode = 0, countRead = 0;

            if (!e.Cancelled)
            {
                if (e.Error == null)
                {
                    try
                    {
                        countBarcode = Convert.ToInt32(e.Result.total);
                        toolStripProgressBar1.Maximum = countBarcode;
                    }
                    catch (Exception) { countBarcode = 0; }

                    DataTable table = ds.Tables["main"];

                    foreach (var item in e.Result.parcels)
                    {
                        DataRow row = table.NewRow();
                        row["Barcode"] = item.Barcode;
                        row["Client_name"] = item.Client_name;
                        row["Country"] = item.Country;
                        row["Country_сode"] = item.Country_сode;
                        row["Label"] = item.Label;
                        row["Label_ID"] = item.Label_ID;
                        row["Scaned"] = item.Scaned;
                        table.Rows.Add(row);

                        if (!toolStripComboBox1.Items.Contains(item.Country))
                        {
                            toolStripComboBox1.Items.Add(item.Country);
                        }

                        countRead++;
                    }

                    if (table.Rows.Count >= countBarcode)
                    {
                        curDoc.id = e.Result.id;
                        button3.Enabled = true;
                        BarcodeBox.ReadOnly = false;
                        toolStripProgressBar1.Value = 0;
                        SetPreScanedFromLocalDb(curDoc.Number);
                        MarkPrintLabel();
                    }
                    else
                    {
                        curPosition += countRead;
                        toolStripProgressBar1.Value = Math.Min(curPosition, toolStripProgressBar1.Maximum);
                        ReadDocument();
                    }

                }
                else
                {
                    MessageBox.Show(e.Error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    button3.Enabled = true;
                    BarcodeBox.ReadOnly = false;
                }
            }

            UpdateStatus();

        }

        private void UpdateStatus()
        {
            toolStripStatusLabelCountBarcode.Text = string.Format("All: {0} Print: {1}", dataGridView1.RowCount, PrintCounter);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            FormAWB fAWB = new FormAWB();
            if (fAWB.ShowDialog() == DialogResult.OK)
            {
                curDoc.Number = fAWB.AWBNumber.Trim();
                labelAWB.Text = curDoc.Number;
                curPosition = 0;
                ds.Tables["main"].Clear();
                toolStripComboBox1.Items.Clear();
                toolStripComboBox1.Items.Add("-");
                BarcodeBox.Focus();
                if (curDoc.Number != "")
                    ReadDocument();
            }
        }

        private void BarcodeBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (curDoc.Number == "")
            {
                return;
            }

            if (e.KeyCode == Keys.Enter && BarcodeBox.Text != "")
            {

                DataTable table = ds.Tables["main"];

                BarcodeBox.Text = BarcodeBox.Text.Replace("$", "");

                var rows = table.AsEnumerable().Where(row => row.Field<string>("Barcode").ToUpper() == BarcodeBox.Text.ToUpper()).Distinct();

                if (rows.Count() == 0)
                {
                    string strQuery = "";
                    string strQuery2 = "";

                    switch (Thread.CurrentThread.CurrentUICulture.Name)
                    {
                        case "ru-RU":
                            strQuery = "Не найден штрих-код. Поискать штрих-код в других накладных?";
                            strQuery2 = "Штрих-код найден в накладной №{0}. Перенести штрих-код в текущий документ?";
                            break;
                        default:
                            strQuery = "Could not find the bar code. Search bar code in other AWB?";
                            strQuery2 = "Barcode found in AWB №{0}. Move barcode in current document?";
                            break;
                    }

                    if (MessageBox.Show(strQuery, "", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        parcelInfoRequest pir = new parcelInfoRequest();
                        pir.token = token;
                        pir.Label_ID = BarcodeBox.Text;
                        pir.Label = false;
                        parcelInfoParcel []p_resultInfo;
                        try
                        {
                            p_resultInfo = soapClient.parcelInfo(pir);
                        }
                        catch (Exception error)
                        {
                            MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }

                        string findAWB = "";
                        parcelInfoParcel resultInfo = new parcelInfoParcel();
                        foreach (var itemResultInfo in p_resultInfo)
                        {
                            resultInfo = itemResultInfo;
                            foreach (var item in itemResultInfo.movement)
                            {
                                findAWB = item.TransportNumber;
                                break;
                            }
                        }

                        if (findAWB != "" && MessageBox.Show(string.Format(strQuery2, findAWB), "", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question) == DialogResult.Yes)
                        {

                            pir.Label = true;
                            try
                            {
                                p_resultInfo = soapClient.parcelInfo(pir);
                                foreach (var itemResultInfo in p_resultInfo)
                                {
                                    resultInfo = itemResultInfo;
                                    break;
                                }
                            }
                            catch (Exception error)
                            {
                                MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            parcelMoveRequest requestMove = new parcelMoveRequest();
                            requestMove.token = token;
                            requestMove.Label_ID = resultInfo.Label_ID;
                            requestMove.Move_TO = curDoc.id;
                            parcelMoveResult resultMove;
                            try
                            {
                                resultMove = soapClient.parcelMove(requestMove);
                                if (dbLog != null)
                                    dbLog.Move(curDoc.Number, BarcodeBox.Text);
                            }
                            catch (Exception error)
                            {
                                MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            if (resultMove.error == "1")
                            {
                                MessageBox.Show(resultMove.error_text, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            DataRow row = table.NewRow();
                            row["Barcode"] = BarcodeBox.Text;
                            row["Client_name"] = resultInfo.Client_name;
                            row["Country"] = resultInfo.Country;
                            row["Country_сode"] = resultInfo.Country_сode;
                            row["Label"] = resultInfo.Label;
                            row["Label_ID"] = resultInfo.Label_ID;
                            row["Scaned"] = resultInfo.Scaned;
                            table.Rows.Add(row);

                            rows.Concat( new[] { row });
                        }
                    }
                }

                foreach (DataRow item in rows)
                {
                    string LabelString = (string)item["Label"];

                    if (PrintLabel(ref LabelString))
                    {
                        if (dbLog != null)
                            dbLog.Add(curDoc.Number, (string)item["Barcode"]);

                        if (!(bool)item["Scaned"])
                            item["PreScaned"] = true;

                        foreach (DataGridViewRow DGRow in dataGridView1.Rows)
                        {
                            if (((string)DGRow.Cells["Barcode"].Value).ToUpper() == BarcodeBox.Text.ToUpper())
                                for (int i = dataGridView1.ColumnCount - 1; i >= 0; i--)
                                    DGRow.Cells[i].Style.BackColor = Color.PaleGreen;
                        }
                    }
                }

                BarcodeBox.Text = "";

                bool NotAllScaned = table.AsEnumerable().Any(row => (row.IsNull("Scaned") ? true : !row.Field<bool>("Scaned")) && (row.IsNull("PreScaned") ? true : !row.Field<bool>("PreScaned")));

                if (!NotAllScaned)
                {
                    string strQuery = "";
                    switch (Thread.CurrentThread.CurrentUICulture.Name)
                    {
                        case "ru-RU":
                            strQuery = "Завершена накладная";
                            break;
                        default:
                            strQuery = "AWB is completed";
                            break;
                    }
                    MessageBox.Show(strQuery, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void pageSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pageSetupDialog1.ShowDialog() == DialogResult.OK)
                PrinterSettingChange = true;
        }

        private bool PrintLabel(ref string LabelString)
        {
            byte[] binaryData;

            try
            {
                binaryData = Convert.FromBase64String(LabelString);
            }
            catch (FormatException)
            {
                MessageBox.Show("Base 64 string length is not 4 or is not an even multiple of 4.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            MemoryStream memStream = new MemoryStream(binaryData);

            pdf.LoadFromStream(memStream);

            if (PrinterSettingChange)
            {
                pdf.PrintDocument.PrinterSettings = printDialog1.PrinterSettings;
                pdf.PrintDocument.DefaultPageSettings = pageSetupDialog1.PageSettings;
                PrinterSettingChange = false;
            }

            try
            {
                pdf.PrintDocument.PrintController = new StandardPrintController();

                pdf.PrintDocument.Print();
                PrintCounter += 1;
                UpdateStatus();
            }
            catch (System.Drawing.Printing.InvalidPrinterException error)
            {
                MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AboutBox1 about = new AboutBox1();
            about.ShowDialog();
        }

        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            toolStripComboBox1.SelectedIndex = 0;
        }

        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selCountry = (string)toolStripComboBox1.SelectedItem;
            if (selCountry != "" && selCountry != "-")
            {
                dv.RowFilter = "Country = '" + selCountry + "'";                
            }
            else
            {
                dv.RowFilter = "";
            }
            
            MarkPrintLabel();
            UpdateStatus();
        }

        private void MarkPrintLabel()
        {
            PrintCounter = 0;
            foreach (DataGridViewRow item in dataGridView1.Rows)
            {
                if (    (item.Cells["Scaned"].Value != DBNull.Value && (bool)item.Cells["Scaned"].Value)
                    ||  (item.Cells["PreScaned"].Value != DBNull.Value && (bool)item.Cells["PreScaned"].Value))
                {
                    PrintCounter++;
                    for (int i = dataGridView1.ColumnCount - 1; i >= 0; i--)
                        item.Cells[i].Style.BackColor = Color.PaleGreen;
                }

            }
        }

        private void buttonScanningReport_Click(object sender, EventArgs e)
        {
            DataTable table = ds.Tables["main"];
            var RowPreScaned = table.AsEnumerable().Where(row => row.IsNull("PreScaned") ? false : row.Field<bool>("PreScaned"));

            if (RowPreScaned.Count() > 0)
            {
                parcelsScaned[] ps = new parcelsScaned[RowPreScaned.Count()];

                int cn = 0;
                foreach (var item in RowPreScaned)
                {
                    parcelsScaned pse = new parcelsScaned();
                    pse.box = "0";
                    pse.pallet = "0";
                    pse.Label_ID = item.Field<string>("Label_ID");
                    ps[cn++] = pse;
                }

                parcelsScanResultRequest rec = new parcelsScanResultRequest();
                rec.token = token;
                rec.parcels = ps;

                string strQueryWebError = "";
                string strQueryOK = "";
                switch (Thread.CurrentThread.CurrentUICulture.Name)
                {
                    case "ru-RU":
                        strQueryWebError = "Не соединения с сервером";
                        strQueryOK = "Сообщение успешно отправлено";
                        break;
                    default:
                        strQueryWebError = "No connection to the server";
                        strQueryOK = "Message sent successfully";
                        break;
                }

                bool Retry = false;
                do
                {
                    Retry = false;

                    try
                    {
                        soapClient.parcelsScanResult(rec);
                        MessageBox.Show(strQueryOK, "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        foreach (var item in RowPreScaned)
                        {
                            item.SetField("PreScaned", false);
                        }                        
                    }
                    catch (System.Net.WebException)
                    {
                        if (MessageBox.Show(strQueryWebError, "Info", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning) == DialogResult.Retry)
                        {
                            Retry = true;
                        }
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(error.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                } while (Retry);

            }
            else
            {
                string strQuery = "";
                switch (Thread.CurrentThread.CurrentUICulture.Name)
                {
                    case "ru-RU":
                        strQuery = "Ничего нового не отсканировано";
                        break;
                    default:
                        strQuery = "Nothing new has been scanned";
                        break;
                }
                MessageBox.Show(strQuery, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void SetPreScanedFromLocalDb(string DocNumber)
        {
            if (dbLog != null)
            {
                List<string> list = dbLog.GetBarcode(DocNumber);

                foreach (DataRow item in ds.Tables["main"].Rows)
                {
                    if (!(bool)item["Scaned"] && list.Exists(x => x == (string)item["Barcode"]))
                    {
                        item["PreScaned"] = true;
                    }
                }
            }
        }

    }
}
