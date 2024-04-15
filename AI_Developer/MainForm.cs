using PSBS_DB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AI_Developer
{
    public partial class MainForm : Form
    {
        bool IsLoggedIn = true;
        string ConfigurationFile = "ProgramConfig.xml";
        string MainConfigFile = "MainConfigs.xml";
        string Utility_Output_File = "Utility_Output.txt";
        bool IsConfigured = false, IsInputPending = true, IsGenerateQueryPending = true, IsCheckFisibilityPending = true, IsProvidingFixPending = true, IsRollbackPending = true;
        List<DataSet> Tickets = new List<DataSet>();

        // current ticket
        int total_input = 0, total_input_done = 0, tbl_save_input_cont = 0;
        string input_field_query, input_field_where;
        private DataTable dbData_Input;
        string con_string = "";
        DBMain dbMain = null;
        private DataTable tbl_Input;
        private DataTable tbl_Update;
        private DataTable tbl_Where;
        private DataTable tbl_Ticket;
        private DataTable tbl_Gen_Query;
        private DataTable tbl_Check_Feasibility;
        private DataTable tbl_Save_Input;
        private List<string> variableList;
        private List<string> columnList;
        private List<string> whereColList;
        private List<string> whereValList;
        private List<string> updateColList;
        private List<string> updateValList;
        private string[] updateQuery;
        private string[] rollbackQuery;

        public MainForm()
        {
            InitializeComponent();
        }

        private void OnMainTabSelectionChanged(object sender, EventArgs e)
        {
            if (tb_main.SelectedIndex > 0 && !IsLoggedIn)
            {
                tb_main.SelectedIndex = 0;
                MessageBox.Show("Please login.", "AI Developer");
            }

        }

        private void btn_login_Click(object sender, EventArgs e)
        {
            if (txt_Username.Text == "Paresh" && txt_Password.Text == "Paresh@2023")
            {
                IsLoggedIn = true;
                lbl_login_alert.Text = "Login Done.";
                btn_login.Enabled = txt_Password.Enabled = txt_Username.Enabled = false;
                tb_main.SelectedIndex = 1;
            }
            else
            {
                MessageBox.Show("incorrect username or password", "Developer");
            }
        }

        private void Btn_Configure_Click(object sender, EventArgs e)
        {
            DataTable data;
            data = (DataTable)DGV_Configuration.DataSource;
            List<DataRow> emptyRows = new List<DataRow>();
            for (int i = 0; i < data.Rows.Count; i++)
            {
                if (data.Rows[i][0].ToString().Trim().Length == 0)
                    emptyRows.Add(data.Rows[i]);
            }
            foreach (var emptyr in emptyRows)
            {
                data.Rows.Remove(emptyr);
            }
            data.AcceptChanges();
            data.WriteXml(ConfigurationFile);
            DGV_Configuration.EditMode = DataGridViewEditMode.EditProgrammatically;
            Btn_Configure.Enabled = false;
            Btn_Modify.Enabled = true;
            IsConfigured = true;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            //create save table input
            tbl_Save_Input = new DataTable("Save_Input");
            tbl_Save_Input.Columns.Add("Sr", 1.GetType());
            tbl_Save_Input.Columns.Add("InputNo", 1.GetType());
            tbl_Save_Input.Columns.Add("Column", "".GetType());
            tbl_Save_Input.Columns.Add("Value", "".GetType());

            Txt_Utility_Output_Path.Text = Utility_Output_File;

            // Check For Existing TicketData
            List<string> files_available = Directory.EnumerateFiles(".").ToList();
            foreach (string item in files_available)
            {
                if (item.EndsWith(".xml") && (Path.GetFileName(item).StartsWith("MR") || Path.GetFileName(item).StartsWith("SCR")))
                {
                    DataSet dataSet = new DataSet();
                    dataSet.ReadXml(item);
                    Tickets.Add(dataSet);
                    Cmb_Tickets.Items.Add(dataSet.DataSetName);
                    Cmb_Ticket_ProvidingFix.Items.Add(dataSet.DataSetName);
                }
            }

            if (File.Exists(ConfigurationFile))
            {
                DataSet set = new DataSet();
                set.ReadXml(ConfigurationFile);
                DGV_Configuration.DataSource = set.Tables[0];
            }
            else
            {
                DataTable tbl = new DataTable("ConnStrConfig");
                tbl.Columns.Add("Sr", 1.GetType());
                tbl.Columns.Add("PlantId", "".GetType());
                tbl.Columns.Add("PlantName", "".GetType());
                tbl.Columns.Add("PlantCode", "".GetType());
                tbl.Columns.Add("ConnectionString", "".GetType());
                tbl.Columns.Add("Tags", "".GetType());
                DGV_Configuration.DataSource = tbl;
                DGV_Configuration.Columns[0].Width = 30;
            }

            DataTable tbl_field_input = new DataTable("InputField_Fix");
            tbl_field_input.Columns.Add("Sr", 1.GetType());
            tbl_field_input.Columns.Add("DatabaseConn", "".GetType());
            tbl_field_input.Columns.Add("Table", "".GetType());
            tbl_field_input.Columns.Add("Column_Display_List", "".GetType());
            tbl_field_input.Columns.Add("Column_List", "".GetType());
            tbl_field_input.Columns.Add("Var_Name_List", "".GetType());
            tbl_field_input.Columns.Add("WhereFieldsId_List", "".GetType());
            DGV_Input_For_Fix.DataSource = tbl_field_input;
            DGV_Input_For_Fix.Columns[0].Width = 30;

            DataTable tbl_field_update = new DataTable("UpdateField_Fix");
            tbl_field_update.Columns.Add("Sr", 1.GetType());
            tbl_field_update.Columns.Add("DatabaseConn", "".GetType());
            tbl_field_update.Columns.Add("Table", "".GetType());
            tbl_field_update.Columns.Add("Column_List", "".GetType());
            tbl_field_update.Columns.Add("Value_List", "".GetType());
            tbl_field_update.Columns.Add("WhereFieldsId_List", "".GetType());
            tbl_field_update.Columns.Add("IsInsert", true.GetType());
            tbl_field_update.Columns.Add("IsDelete", true.GetType());
            DGV_Update_Fields_For_Fix.DataSource = tbl_field_update;
            DGV_Update_Fields_For_Fix.Columns[0].Width = 30;

            DataTable tbl_field_where = new DataTable("WhereField_Fix");
            tbl_field_where.Columns.Add("Sr", 1.GetType());
            tbl_field_where.Columns.Add("Column_Name", "".GetType());
            tbl_field_where.Columns.Add("Value", "".GetType());
            DGV_Where_Field_For_Fix.DataSource = tbl_field_where;
            DGV_Where_Field_For_Fix.Columns[0].Width = 30;

            DataTable tbl_field_feasibility = new DataTable("Check_Feasibility");
            tbl_field_feasibility.Columns.Add("Sr", 1.GetType());
            tbl_field_feasibility.Columns.Add("Input_Rec_Id", "".GetType());
            tbl_field_feasibility.Columns.Add("Column", "".GetType());
            tbl_field_feasibility.Columns.Add("Condition", "".GetType());
            DGV_Check_Feasibility.DataSource = tbl_field_feasibility;
            DGV_Check_Feasibility.Columns[0].Width = 30;

            DataTable tbl_GeneratedQueryLog = new DataTable();
            tbl_GeneratedQueryLog.Columns.Add("Sr", 1.GetType());
            tbl_GeneratedQueryLog.Columns.Add("TicketNo", "".GetType());
            tbl_GeneratedQueryLog.Columns.Add("Database_Id", 1.GetType());
            tbl_GeneratedQueryLog.Columns.Add("Fix_Query", "".GetType());
            tbl_GeneratedQueryLog.Columns.Add("Rollback_Query", "".GetType());
            tbl_GeneratedQueryLog.Columns.Add("Fix_Given_At", "".GetType());
            dgv_Gen_Query_Log.DataSource = tbl_GeneratedQueryLog;
            dgv_Gen_Query_Log.Columns[0].Width = 30;

            if (File.Exists(MainConfigFile))
            {
                DataSet set = new DataSet();
                set.ReadXml(MainConfigFile);
                DataTable tmp = set.Tables["Settings"];

                if (tmp != null && tmp.Rows.Count > 0)
                {

                    IsConfigured = Convert.ToBoolean(tmp.Rows[0][0]); // IsConfig

                }
            }

            if (IsConfigured)
            {
                DGV_Configuration.EditMode = DataGridViewEditMode.EditProgrammatically;
                Btn_Configure.Enabled = false;
            }
            else
            {
                DGV_Configuration.EditMode = DataGridViewEditMode.EditOnKeystroke;
                Btn_Modify.Enabled = false;
            }
        }

        private void DGV_Configuration_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void Btn_Modify_Click(object sender, EventArgs e)
        {
            DGV_Configuration.EditMode = DataGridViewEditMode.EditOnKeystroke;
            Btn_Modify.Enabled = false;
            Btn_Configure.Enabled = true;
            IsConfigured = false;
        }

        private void OnFormCloseing(object sender, FormClosedEventArgs e)
        {
            DataSet dataSet = new DataSet("MainConfigs");
            DataTable dataConfig = new DataTable("Settings");
            dataConfig.Columns.Add("IsConfigured", true.GetType());

            DataRow r = dataConfig.NewRow();

            r[0] = IsConfigured; // IsConfig

            dataConfig.Rows.Add(r);

            dataConfig.AcceptChanges();

            dataSet.Tables.Add(dataConfig);
            dataSet.WriteXml(MainConfigFile);
        }

        private void Btn_Take_Input_Click(object sender, EventArgs e)
        {
            if (IsInputPending)
            {
                if (Tickets.Count > Cmb_Ticket_ProvidingFix.SelectedIndex)
                {
                    DataSet ticketSet = Tickets[Cmb_Ticket_ProvidingFix.SelectedIndex];
                    tbl_Input = ticketSet.Tables["InputField_Fix"];
                    if (total_input == 0)
                    {
                        total_input = tbl_Input.Rows.Count;
                        tbl_save_input_cont = 1;
                        tbl_Save_Input.Clear();
                    }
                    if (total_input_done > 0)
                    {
                        // save input
                        ushort column_Select_Id = (ushort)DGV_Input_Fix.Columns["Select"].Index;
                        // take selected rows
                        for (int i = 0; i < DGV_Input_Fix.Rows.Count; i++)
                        {
                            if (Convert.ToBoolean(DGV_Input_Fix.Rows[i].Cells[column_Select_Id].Value) == false)
                                continue;

                            // if selected

                            for (int j = 0; j < dbData_Input.Columns.Count /* && columnList.Contains(dbData_Input.Columns[j].ColumnName) */ ; j++)
                            {
                                DataRow r = tbl_Save_Input.NewRow();
                                r[0] = tbl_save_input_cont++;
                                r[1] = total_input_done;
                                r[2] = dbData_Input.Columns[j].ColumnName;
                                r[3] = dbData_Input.Rows[i][j];
                                tbl_Save_Input.Rows.Add(r);
                            }
                        }
                        tbl_Save_Input.AcceptChanges();
                    }

                    if (total_input > 0 && total_input > total_input_done)
                    {
                        tbl_Update = ticketSet.Tables["UpdateField_Fix"];
                        tbl_Where = ticketSet.Tables["WhereField_Fix"];
                        tbl_Ticket = ticketSet.Tables["TicketDetails"];
                        tbl_Check_Feasibility = ticketSet.Tables["Check_Feasibility"];

                        DataTable tbl_ConnectionString = (DataTable)DGV_Configuration.DataSource;
                        DataRow[] conRows = tbl_ConnectionString.Select("Sr=" + tbl_Input.Rows[total_input_done]["DatabaseConn"]);
                        con_string = "";

                        if (conRows.Length > 0)
                            con_string = conRows[0]["ConnectionString"].ToString();
                        else
                        {
                            MessageBox.Show("Database Connection string not found.");
                            return;
                        }

                        dbMain = new DBMain(con_string, ".");

                        Rtxt_Querybox.Text = "-- INPUT" + Environment.NewLine;

                        Lbl_Status.Text = "Status : Input Sr." + tbl_Input.Rows[total_input_done][0];

                        if (variableList == null)
                            variableList = new List<string>();
                        if (columnList == null)
                            columnList = new List<string>();
                        //variableList = tbl_Input.Rows[total_input_done]["Var_Name_List"].ToString().Split(',').ToList();
                        //columnList = tbl_Input.Rows[total_input_done]["Column_List"].ToString().Split(',').ToList();
                        foreach (string s in tbl_Input.Rows[total_input_done]["Var_Name_List"].ToString().Split(','))
                        {
                            if (!variableList.Contains(s))
                            {
                                variableList.Add(s);

                            }
                        }
                        foreach (string s in tbl_Input.Rows[total_input_done]["Column_List"].ToString().Split(','))
                        {
                            if (!columnList.Contains(s))
                            {
                                columnList.Add(s);
                            }
                        }

                        for (int i = 0; i < variableList.Count; i++)
                        {
                            Rtxt_Querybox.Text += "Declare @" + variableList[i] + " varchar = ''; -- " + columnList[i] + Environment.NewLine;
                        }

                        input_field_query = "Select " + tbl_Input.Rows[total_input_done]["Column_Display_List"] + " From " + tbl_Input.Rows[total_input_done]["Table"];

                        string whereRecs = tbl_Input.Rows[total_input_done]["WhereFieldsId_List"].ToString();
                        if (whereRecs.Trim().Length > 0)
                        {
                            DataRow[] rows = tbl_Where.Select("Sr in (" + whereRecs + ")");
                            if (rows != null && rows.Length > 0)
                            {
                                input_field_query += " Where ";
                            }
                            for (int l = 0; l < rows.Length; l++)
                            {
                                if (rows[l][2].ToString().Contains("var"))
                                {
                                    if (variableList.Contains(rows[l][2]))
                                    {
                                        int ind = variableList.IndexOf(rows[l][2].ToString());
                                        string colm = columnList[ind];
                                        DataRow[] saveIn = tbl_Save_Input.Select($"InputNo={(total_input_done)} And Column = '{colm}'");
                                        string str_values = "";
                                        if (saveIn.Length > 0)
                                            str_values = "'";
                                        for (int j = 0; j < saveIn.Length; j++)
                                        {
                                            str_values += saveIn[j][3].ToString() + "','";
                                        }
                                        if (saveIn.Length > 0)
                                            str_values = str_values.Substring(0, str_values.Length - 2);

                                        input_field_query += rows[l][1] + " in (" + str_values + ") And ";
                                    }
                                }
                                else
                                {
                                    input_field_query += rows[l][1] + " = '" + rows[l][2] + "' And ";
                                }
                            }
                            if (rows != null && rows.Length > 0)
                            {
                                input_field_query = input_field_query.Substring(0, input_field_query.Length - 4);
                            }
                        }
                        dbData_Input = dbMain.GetData(input_field_query);

                        DataTable tbl_Filter = new DataTable("Tbl_Filter");
                        tbl_Filter.Columns.Add("Columns", "".GetType());
                        tbl_Filter.Columns.Add("Filer", "".GetType());
                        foreach (DataColumn item in dbData_Input.Columns)
                        {
                            DataRow r = tbl_Filter.NewRow();
                            r[0] = item.ColumnName;
                            if (item.DataType == "".GetType())
                                r[1] = "%";
                            else if (item.DataType == 1.GetType())
                                r[1] = ">0";
                            else if (item.DataType == (5.5).GetType())
                                r[1] = ">0";
                            else if (item.DataType == DateTime.Now.GetType())
                                r[1] = "> '1900-1-1' ";
                            tbl_Filter.Rows.Add(r);
                        }
                        tbl_Filter.AcceptChanges();

                        DGV_Input_Fix.DataSource = dbData_Input;
                        if (!DGV_Input_Fix.Columns.Contains("Select"))
                        {
                            DataGridViewCheckBoxColumn col = new DataGridViewCheckBoxColumn();
                            col.Name = "Select";
                            col.DataPropertyName = "Slct";
                            col.DisplayIndex = 0;
                            col.Width = 30;
                            DGV_Input_Fix.Columns.Add(col);
                        }

                        DGV_Input_Filter.DataSource = tbl_Filter;

                        DGV_Input_Filter.Columns[0].ReadOnly = true;

                        Rtxt_Querybox.Text += "Select * From " + tbl_Input.Rows[total_input_done]["Table"] + ";" + Environment.NewLine;

                        total_input_done++;
                    }
                    else
                    {
                        if (total_input == total_input_done)
                        {
                            IsInputPending = false;
                            Btn_Check_Fisibility.Enabled = true;
                            Btn_Take_Input.Enabled = false;
                        }
                    }
                }
            }
        }

        private void Btn_Gen_Query_Click(object sender, EventArgs e)
        {
            DataTable tbl_gen_query = (DataTable)dgv_Gen_Query_Log.DataSource;
            DataRow r = tbl_gen_query.NewRow();
            if (!IsGenerateQueryPending)
            {
                // prepare queries
                updateQuery = new string[tbl_Update.Rows.Count];
                rollbackQuery = new string[tbl_Update.Rows.Count];

                for (int k = 0; k < tbl_Update.Rows.Count; k++)
                {
                    updateQuery[k] = "Update ";
                    rollbackQuery[k] = "Update ";

                    DataTable tbl_ConnectionString = (DataTable)DGV_Configuration.DataSource;
                    DataRow[] conRows = tbl_ConnectionString.Select("Sr=" + tbl_Update.Rows[k]["DatabaseConn"]);
                    con_string = "";

                    if (conRows.Length > 0)
                        con_string = conRows[0]["ConnectionString"].ToString();
                    else
                    {
                        MessageBox.Show("Database Connection string not found.");
                        return;
                    }

                    dbMain = new DBMain(con_string, ".");

                    updateQuery[k] += tbl_Update.Rows[k]["Table"];
                    rollbackQuery[k] += tbl_Update.Rows[k]["Table"];

                    // set values

                    updateColList = tbl_Update.Rows[k]["Column_List"].ToString().Split(',').ToList();
                    updateValList = tbl_Update.Rows[k]["Value_List"].ToString().Split(',').ToList();

                    for (int l = 0; l < updateColList.Count; l++)
                    {
                        string setValue = "";
                        if (updateValList[l].Contains("var") || updateValList[l].Contains("Var"))
                        {
                            if (variableList.Contains(updateValList[l]))
                            {
                                int ind = variableList.IndexOf(updateValList[l].ToString());
                                string colm = columnList[ind];
                                string temp_C = "0";
                                temp_C = tbl_Input.Select($"Var_Name_List like '%{updateValList[l]}%'")[0][0].ToString();

                                DataRow[] saveIn_c = tbl_Save_Input.Select($"InputNo={(temp_C)} And Column = '{colm}'");
                                string str_values_c = "";
                                if (saveIn_c.Length > 0)
                                    str_values_c = "";
                                for (int j = 0; j < saveIn_c.Length; j++)
                                {
                                    str_values_c += saveIn_c[j][3].ToString() + ",'";
                                }
                                if (saveIn_c.Length > 0)
                                    str_values_c = str_values_c.Substring(0, str_values_c.Length - 2);

                                setValue = str_values_c;
                            }
                        }
                        else
                            setValue = updateValList[l];

                        if (updateQuery[k].Contains("Set"))
                            updateQuery[k] += updateColList[l] + " = '" + setValue + "', ";
                        else
                            updateQuery[k] += " Set " + updateColList[l] + " = '" + setValue + "', ";

                        string temp_k = "0";
                        temp_k = tbl_Input.Select($"Var_Name_List like '%{updateValList[l]}%'")[0][0].ToString();

                        DataRow[] saveIn = tbl_Save_Input.Select($"InputNo={(temp_k)} And Column = '{updateColList[l]}'");
                        string str_values = "";
                        if (saveIn.Length > 0)
                            str_values = "'";
                        for (int j = 0; j < saveIn.Length; j++)
                        {
                            str_values += saveIn[j][3].ToString() + "','";
                        }
                        if (saveIn.Length > 0)
                            str_values = str_values.Substring(0, str_values.Length - 2);

                        if (rollbackQuery[k].Contains("Set"))
                            rollbackQuery[k] += updateColList[l] + " = " + str_values + ", ";
                        else
                            rollbackQuery[k] += " Set " + updateColList[l] + " = " + str_values + ", ";

                    }

                    updateQuery[k] = updateQuery[k].Substring(0, updateQuery[k].Length - 2);
                    rollbackQuery[k] = rollbackQuery[k].Substring(0, rollbackQuery[k].Length - 2);

                    //where fields
                    string whereRecs = tbl_Update.Rows[k]["WhereFieldsId_List"].ToString();
                    DataRow[] rows = tbl_Where.Select("Sr in (" + whereRecs + ")");
                    if (rows != null && rows.Length > 0)
                    {
                        updateQuery[k] += " Where ";
                        rollbackQuery[k] += " Where ";
                    }
                    for (int l = 0; l < rows.Length; l++)
                    {
                        if (rows[l][2].ToString().Contains("var"))
                        {
                            if (variableList.Contains(rows[l][2]))
                            {
                                int ind = variableList.IndexOf(rows[l][2].ToString());
                                string colm = columnList[ind];
                                string temp_k = "0";
                                temp_k = tbl_Input.Select($"Var_Name_List like '%{rows[l][2]}%'")[0][0].ToString();

                                DataRow[] saveIn = tbl_Save_Input.Select($"InputNo={(temp_k)} And Column = '{colm}'");
                                string str_values = "";
                                if (saveIn.Length > 0)
                                    str_values = "'";
                                for (int j = 0; j < saveIn.Length; j++)
                                {
                                    str_values += saveIn[j][3].ToString() + "','";
                                }
                                if (saveIn.Length > 0)
                                    str_values = str_values.Substring(0, str_values.Length - 2);

                                updateQuery[k] += rows[l][1] + " in (" + str_values + ") And ";
                                rollbackQuery[k] += rows[l][1] + " in (" + str_values + ") And ";
                            }
                        }
                        else
                        {
                            updateQuery[k] += rows[l][1] + " = '" + rows[l][2] + "' And ";
                            rollbackQuery[k] += rows[l][1] + " = '" + rows[l][2] + "' And ";
                        }
                    }
                    if (rows != null && rows.Length > 0)
                    {
                        updateQuery[k] = updateQuery[k].Substring(0, updateQuery[k].Length - 4);
                        rollbackQuery[k] = rollbackQuery[k].Substring(0, rollbackQuery[k].Length - 4);
                    }
                    Rtxt_Querybox.Text += Environment.NewLine + "-- Update";
                    Rtxt_Querybox.Text += Environment.NewLine + "--** Rollback Query";
                    Rtxt_Querybox.Text += Environment.NewLine + rollbackQuery[k] + Environment.NewLine;
                    r["TicketNo"] = "";
                    r["TicketNo"] = "";
                    r["Fix_Query"] = "";
                    r["Rollback_Query"] = "";
                    r["Fix_Given_At"] = "";
                    Rtxt_Querybox.Text += Environment.NewLine + "--** Fix Query";
                    Rtxt_Querybox.Text += Environment.NewLine + updateQuery[k] + Environment.NewLine;

                    
                }

                Btn_Gen_Query.Enabled = false;
                IsProvidingFixPending = false;
                Btn_Provide_Fix.Enabled = true;
            }
        }

        private void Btn_Check_Fisibility_Click(object sender, EventArgs e)
        {
            if (!IsInputPending)
            {
                Rtxt_Querybox.Text += Environment.NewLine + "-- Feasibility Checking ";
                // prepare search record query
                string[] queries = new string[total_input_done];
                bool mainFeasibility = true;
                bool[] feasibility = new bool[total_input_done];
                for (int i = 0; i < total_input_done; i++)
                {
                    queries[i] = "Select " + tbl_Input.Rows[i]["Column_Display_List"] + " From " + tbl_Input.Rows[i]["Table"];
                    //DataRow[] rows = tbl_Save_Input.Select("InputNo=" + (i + 1));
                    //if (rows.Length > 0)
                    //    queries[i] += " Where ";
                    //for (int j = 0; j < rows.Length; j++)
                    //{
                    //    queries[i] += rows[j][2] + " = '" + rows[j][3] + "' And ";
                    //}
                    //if (rows.Length > 0)
                    //    queries[i] = queries[i].Substring(0, queries[i].Length - 4);

                    // feasibility table
                    DataRow[] rows_f = tbl_Check_Feasibility.Select("Input_Rec_Id=" + (i + 1));
                    if (rows_f.Length > 0)
                        queries[i] += " Where ";
                    for (int j = 0; j < rows_f.Length; j++)
                    {
                        if (rows_f[j][3].ToString().Contains("var"))
                        {
                            if (variableList.Contains(rows_f[j][3]))
                            {
                                int ind = variableList.IndexOf(rows_f[j][3].ToString());
                                string colm = columnList[ind];
                                DataRow[] saveIn = tbl_Save_Input.Select($"InputNo={(i + 1)} And Column = '{colm}'");
                                string str_values = "";
                                if (saveIn.Length > 0)
                                    str_values = "'";
                                for (int k = 0; k < saveIn.Length; k++)
                                {
                                    str_values += saveIn[k][3].ToString() + "','";
                                }
                                if (saveIn.Length > 0)
                                    str_values = str_values.Substring(0, str_values.Length - 2);

                                queries[i] += rows_f[j][2] + " in (" + str_values + ") And ";
                            }
                        }
                        else
                        {
                            queries[i] += rows_f[j][2] + " " + rows_f[j][3] + " And ";
                        }

                    }
                    if (rows_f.Length > 0)
                        queries[i] = queries[i].Substring(0, queries[i].Length - 4);


                    Rtxt_Querybox.Text += Environment.NewLine + queries[i];

                    DataTable tbl_ConnectionString = (DataTable)DGV_Configuration.DataSource;
                    DataRow[] conRows = tbl_ConnectionString.Select("Sr=" + tbl_Input.Rows[i]["DatabaseConn"]);
                    con_string = "";

                    if (conRows.Length > 0)
                        con_string = conRows[0]["ConnectionString"].ToString();
                    else
                    {
                        MessageBox.Show("Database Connection string not found.");
                        return;
                    }

                    // take database
                    dbMain = new DBMain(con_string, ".");

                    // checking feasibility
                    DataTable dbTab = dbMain.GetData(queries[i]);
                    if (dbTab != null && dbTab.Rows.Count > 0)
                        feasibility[i] = true;
                    else
                    {
                        feasibility[i] = false;
                        mainFeasibility = false;
                    }

                    Rtxt_Querybox.Text += " -- Feasibility -> " + feasibility[i] + Environment.NewLine;
                }

                if (!mainFeasibility)
                {
                    MessageBox.Show("This fix is not feasible.");
                }
                else
                {
                    Btn_Check_Fisibility.Enabled = false;
                    IsGenerateQueryPending = false;
                    Btn_Gen_Query.Enabled = true;
                }
            }
        }

        private void Btn_Utility_Backup_STP_All_Db_Click(object sender, EventArgs e)
        {
            DataTable data = (DataTable)DGV_Configuration.DataSource;
            string inputdata = Txt_UtilityInputTxt.Text;
            string all_db_data = "";
            foreach (DataRow r in data.Rows)
            {
                
                string constr = r["ConnectionString"].ToString();
                string dbname = constr.Split(new string[] { "Initial Catalog=" }, StringSplitOptions.None)[1];
                dbname = dbname.Split(';')[0];
                all_db_data += Environment.NewLine + $"Use {dbname};" + Environment.NewLine;
                string all_stp_data = "";
                string folder_Path = Path.Combine(Path.GetDirectoryName(Utility_Output_File), dbname);
                if (!Directory.Exists(folder_Path))
                {
                    Directory.CreateDirectory(folder_Path);
                }
                foreach (string inp in inputdata.Split('\n'))
                {
                    string stp_name = inp.Trim();
                    string file_path = Path.Combine(folder_Path, stp_name);

                    SqlConnection myConnection = new SqlConnection(constr);
                    SqlCommand myCommand = new SqlCommand();
                    SqlDataReader reader;
                    myCommand.Connection = myConnection;
                    myConnection.Open();
                    myCommand.CommandText = $@"SELECT DISTINCT
                                    o.name AS Object_Name,o.type_desc,m.definition
                                    FROM sys.sql_modules        m
                                        INNER JOIN sys.objects  o ON m.object_id=o.object_id
                                        INNER JOIN sys.schemas  s ON o.schema_id=s.schema_id
                                    WHERE o.name Like '{stp_name}'";
                    reader = myCommand.ExecuteReader();
                    if (reader.HasRows)
                    {
                        DataTable t = new DataTable();
                        for (int j = 0; j < reader.FieldCount; j++)
                        {
                            t.Columns.Add(reader.GetName(j), reader.GetFieldType(j));
                        }
                        while (reader.Read())
                        {
                            DataRow myRow = t.NewRow();
                            for (int j = 0; j < reader.FieldCount; j++)
                            {
                                myRow[j] = reader.GetValue(j);
                            }
                            t.Rows.Add(myRow);
                        }
                        if (t.Rows.Count > 0)
                        {
                            string obj_data = "";
                            if (Rd_Utility_Alter.Checked)
                                obj_data = t.Rows[0][2].ToString().Replace("CREATE PROCEDURE [dbo].", "ALTER PROCEDURE [dbo].");
                            else
                                obj_data = t.Rows[0][2].ToString();
                            File.WriteAllText(file_path + ".sql", obj_data);
                            all_stp_data += Environment.NewLine + "Go" + Environment.NewLine + obj_data + Environment.NewLine + "Go" + Environment.NewLine;
                        }
                    }
                    myConnection.Close();
                }
                File.WriteAllText(Path.Combine(folder_Path, "All Stps") + ".sql", all_stp_data);
                all_db_data += all_stp_data;
            }
            File.WriteAllText(Utility_Output_File, all_db_data);
            MessageBox.Show("Object Backup is stored.", "AI Developer");
        }

        private void Btn_Utility_Output_Path_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Application.StartupPath;
            openFileDialog.Multiselect = false;
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                Utility_Output_File = openFileDialog.FileName;
                Txt_Utility_Output_Path.Text = Utility_Output_File;
            }
        }

        private void Btn_Fix_Path_Browse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Application.StartupPath;
            openFileDialog.Multiselect = false;
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                Utility_Output_File = openFileDialog.FileName;
            }
        }

        private void DGV_Input_Filter_Cell_Validating(object sender, DataGridViewCellValidatingEventArgs e)
        {

        }

        private void DGV_Input_Filter_Cell_Leav(object sender, DataGridViewCellEventArgs e)
        {
            var cells = DGV_Input_Filter.SelectedCells;
            if (cells.Count > 0)
            {
                DataTable data = (DataTable)DGV_Input_Filter.DataSource;
                int selectedRow = cells[0].RowIndex;
                int selectedCol = cells[0].ColumnIndex;
                input_field_where = "";
                if (selectedCol == 1)
                {
                    foreach (DataRow item in data.Rows)
                    {
                        if (item[1].ToString().Contains("%"))
                            input_field_where += item[0] + " Like '" + item[1] + "' And ";
                        else
                            input_field_where += item[0] + " " + item[1] + " And ";
                    }
                    input_field_where = input_field_where.Substring(0, input_field_where.Length - 4);
                    if (dbMain != null)
                    {
                        DataRow[] rr = dbData_Input.Select(input_field_where);
                        if (rr.Length > 0)
                        {
                            DataTable tbl = rr.CopyToDataTable();
                            DGV_Input_Fix.DataSource = tbl;
                            DGV_Input_Fix.Update();
                        }
                    }
                }
            }
        }

        private void Btn_Provide_Fix_Click(object sender, EventArgs e)
        {
            if (!IsProvidingFixPending)
            {
                for (int k = 0; k < tbl_Update.Rows.Count; k++)
                {

                    DataTable tbl_ConnectionString = (DataTable)DGV_Configuration.DataSource;
                    DataRow[] conRows = tbl_ConnectionString.Select("Sr=" + tbl_Update.Rows[k]["DatabaseConn"]);
                    con_string = "";

                    if (conRows.Length > 0)
                        con_string = conRows[0]["ConnectionString"].ToString();
                    else
                    {
                        MessageBox.Show("Database Connection string not found.");
                        return;
                    }

                    dbMain = new DBMain(con_string, ".");

                    if (dbMain.ExeNonQuery(updateQuery[k]))
                    {
                        Rtxt_Querybox.Text += Environment.NewLine + "-- Provided Fix On " + DateTime.Now.ToString("F") + Environment.NewLine;
                    }
                    else
                    {
                        Rtxt_Querybox.Text += Environment.NewLine + "-- Error in Provided Fix On " + DateTime.Now.ToString("F") + Environment.NewLine;
                    }
                }

                Btn_Provide_Fix.Enabled = false;
                Btn_Rollback.Enabled = true;
                IsRollbackPending = false;


            }
        }

        private void Btn_Rollback_Click(object sender, EventArgs e)
        {
            if (!IsRollbackPending)
            {
                for (int k = 0; k < tbl_Update.Rows.Count; k++)
                {

                    DataTable tbl_ConnectionString = (DataTable)DGV_Configuration.DataSource;
                    DataRow[] conRows = tbl_ConnectionString.Select("Sr=" + tbl_Update.Rows[k]["DatabaseConn"]);
                    con_string = "";

                    if (conRows.Length > 0)
                        con_string = conRows[0]["ConnectionString"].ToString();
                    else
                    {
                        MessageBox.Show("Database Connection string not found.");
                        return;
                    }

                    dbMain = new DBMain(con_string, ".");

                    if (dbMain.ExeNonQuery(rollbackQuery[k]))
                    {
                        Rtxt_Querybox.Text += Environment.NewLine + "-- Rollbacked Fix On " + DateTime.Now.ToString("F") + Environment.NewLine;
                    }
                    else
                    {
                        Rtxt_Querybox.Text += Environment.NewLine + "-- Error in Rollbacked Fix On " + DateTime.Now.ToString("F") + Environment.NewLine;
                    }
                }

                Btn_Rollback.Enabled = false;
                Btn_Provide_Fix.Enabled = true;
            }
        }

        private void btn_Save_ticket_ca_Click(object sender, EventArgs e)
        {
            string ticketNo = Txt_Ticket_No.Text;
            string ticketDesc = Txt_Ticket_Desc.Text;
            string ticketCa = Txt_Ticket_CA.Text;

            string errorMsg = "";

            if (ticketNo.Trim().Length == 0)
                errorMsg += "\nEnter valid ticket no.";

            if (ticketDesc.Trim().Length == 0)
                errorMsg += "\nEnter valid ticket desc";

            if (ticketCa.Trim().Length == 0)
                errorMsg += "\nEnter valid ticket CA";

            if (errorMsg.Trim().Length > 0)
            {
                MessageBox.Show(errorMsg, "Error | AI Developer");
                return;
            }

            DataSet dataSet = null;
            tbl_Input = (DataTable)DGV_Input_For_Fix.DataSource;
            if (tbl_Input.DataSet == null)
                dataSet = new DataSet(ticketNo);
            else
                dataSet = tbl_Input.DataSet;
            dataSet.DataSetName = ticketNo;
            tbl_Update = (DataTable)DGV_Update_Fields_For_Fix.DataSource;
            tbl_Where = (DataTable)DGV_Where_Field_For_Fix.DataSource;
            tbl_Check_Feasibility = (DataTable)DGV_Check_Feasibility.DataSource;
            tbl_Ticket = new DataTable("TicketDetails");

            tbl_Ticket.Columns.Add("No");
            tbl_Ticket.Columns.Add("Desc");
            tbl_Ticket.Columns.Add("CA");

            DataRow r = tbl_Ticket.NewRow();
            r[0] = ticketNo;
            r[1] = ticketDesc;
            r[2] = ticketCa;

            tbl_Ticket.Rows.Add(r);
            tbl_Ticket.AcceptChanges();

            if (dataSet.Tables.Contains(tbl_Ticket.TableName))
                dataSet.Tables.Remove(tbl_Ticket.TableName);
            if (dataSet.Tables.Contains(tbl_Input.TableName))
                dataSet.Tables.Remove(tbl_Input);
            if (dataSet.Tables.Contains(tbl_Update.TableName))
                dataSet.Tables.Remove(tbl_Update);
            if (dataSet.Tables.Contains(tbl_Where.TableName))
                dataSet.Tables.Remove(tbl_Where);
            if (dataSet.Tables.Contains(tbl_Check_Feasibility.TableName))
                dataSet.Tables.Remove(tbl_Check_Feasibility);

            dataSet.Tables.Add(tbl_Ticket);
            dataSet.Tables.Add(tbl_Input);
            dataSet.Tables.Add(tbl_Update);
            dataSet.Tables.Add(tbl_Where);
            dataSet.Tables.Add(tbl_Check_Feasibility);

            dataSet.WriteXml(ticketNo + ".xml");
        }

        private void Chk_Modify_CheckedChanged(object sender, EventArgs e)
        {
            if (Chk_Modify.Checked == true)
            {
                Cmb_Tickets.Enabled = true;
            }
            else
            {
                Cmb_Tickets.Enabled = false;
            }
        }

        private void Cmb_Tickets_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (Tickets.Count > Cmb_Tickets.SelectedIndex)
            {
                DataSet ticketSet = Tickets[Cmb_Tickets.SelectedIndex];
                tbl_Input = ticketSet.Tables["InputField_Fix"];
                tbl_Update = ticketSet.Tables["UpdateField_Fix"];
                tbl_Where = ticketSet.Tables["WhereField_Fix"];
                tbl_Ticket = ticketSet.Tables["TicketDetails"];
                tbl_Check_Feasibility = ticketSet.Tables["Check_Feasibility"];

                Txt_Ticket_No.Text = tbl_Ticket.Rows[0][0].ToString();
                Txt_Ticket_Desc.Text = tbl_Ticket.Rows[0][1].ToString();
                Txt_Ticket_CA.Text = tbl_Ticket.Rows[0][2].ToString();

                DGV_Input_For_Fix.DataSource = tbl_Input;
                DGV_Update_Fields_For_Fix.DataSource = tbl_Update;
                DGV_Where_Field_For_Fix.DataSource = tbl_Where;
                DGV_Check_Feasibility.DataSource = tbl_Check_Feasibility;
            }
        }

        private void Cmb_Ticket_ProvidingFix_SelectedIndexChanged(object sender, EventArgs e)
        {
            Lbl_Status.Text = "Status : Pending Input";
            Btn_Take_Input.Enabled = true;
            Btn_Gen_Query.Enabled = false;
            Btn_Check_Fisibility.Enabled = false;
            Btn_Provide_Fix.Enabled = false;

            IsProvidingFixPending = true;
            IsGenerateQueryPending = true;
            IsCheckFisibilityPending = true;
            IsInputPending = true;
            IsRollbackPending = true;

            variableList = null;
            columnList = null;
            whereColList = null;
            whereValList = null;
            updateColList = null;
            updateValList = null;
            updateQuery = null;
            rollbackQuery = null;

            DGV_Input_Fix.DataSource = null;
            DGV_Input_Filter.DataSource = null;

            tbl_Save_Input.Rows.Clear();


            if (Tickets.Count > Cmb_Ticket_ProvidingFix.SelectedIndex)
            {
                DataSet ticketSet = Tickets[Cmb_Ticket_ProvidingFix.SelectedIndex];
                tbl_Input = ticketSet.Tables["InputField_Fix"];
                tbl_Update = ticketSet.Tables["UpdateField_Fix"];
                tbl_Where = ticketSet.Tables["WhereField_Fix"];
                tbl_Ticket = ticketSet.Tables["TicketDetails"];
                tbl_Check_Feasibility = ticketSet.Tables["Check_Feasibility"];

                Txt_Fix_No.Text = tbl_Ticket.Rows[0][0].ToString();
                Txt_Fix_Desc.Text = tbl_Ticket.Rows[0][1].ToString();
                Txt_Fix_Ca.Text = tbl_Ticket.Rows[0][2].ToString();

                total_input_done = 0;

                Rtxt_Querybox.Text = "";
            }
        }
    }
}
