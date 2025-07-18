﻿using Dapper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using PMS_V4_SAP_integration.Models;
using SAPbobsCOM;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace PMS_V4_SAP_integration.Helper
{
    public static class TaskHelper
    {
        public static void Log(ListView logListView, string message)
        {
            // Get the current time
            string currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Create ListViewItem with time and message
            ListViewItem item = new ListViewItem(new[] { currentTime, message });

            // Add the ListViewItem to the ListView
            logListView.Items.Add(item).EnsureVisible();

            // credit memo and commission go to ORIN table in SAP (in the table, the series referes to DOCNUM )
            // invoice go to OINV table in SAP
        }

        //connect SQL
        public static void ConnectToDatabase(IConfiguration _configuration, ConnectSAP connectSAP, ListView logListView, TextBox textboxView)
        {
            Log(logListView, "Attempting to connect SQL database");
            var connectionString = _configuration.GetConnectionString("Default");

            try
            {
                using (var connectSQL = new SqlConnection(connectionString))
                {
                    connectSQL.Open(); // Asynchronous connectSQL open
                    Log(logListView, "Successfully connected to the database.");

                    // Pass the open connectSQL to Execute_Invoice_ScriptAsync
                    Log(logListView, "Adding Invoice...");
                    Execute_Invoice_Script(connectSQL, logListView, textboxView, connectSAP);  //disabled for testing

                    Log(logListView, "Adding Credit Note...");
                    Execute_CreditNote_Script(connectSQL, logListView, textboxView, connectSAP);

                    Log(logListView, "Adding Commission...");
                    Execute_Commission_Script(connectSQL, logListView, textboxView, connectSAP);

                    Log(logListView, "Adding RD Commission...");
                    Execute_RDCommission_Script(connectSQL, logListView, textboxView, connectSAP);

                    Log(logListView, "Process completed! ");
                    textboxView.Text = "Process completed!";
                    connectSAP.oCompany.Disconnect();

                    // Check if disconnected successfully
                    if (!connectSAP.oCompany.Connected)
                    {
                        Log(logListView, "Disconnected from SAP B1.");
                    }
                    else
                    {
                        Log(logListView, "Failed to disconnect from SAP B1.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log(logListView, $"Error connecting to the database: {ex.Message}");
                return;
            }
        }

        //connect SAP
        public static ConnectSAP ConnectSAP(IConfiguration _configuration, ListView logListView)
        {
            Log(logListView, $"Attempting to connect SAP database {_configuration.GetSection("SAPBusinessOneConfig:CompanyDB").Value}");

            ConnectSAP connectSQL = new ConnectSAP(_configuration);

            int connectResult = connectSQL.SAPConnect(); // Synchronous execution of Connect method

            if (connectResult == 0)
            {
                Log(logListView, "Successfully connected to the SAP database.");
                Log(logListView, "Please press Post button to continue.");
                return connectSQL;
            }
            else if (connectResult == 100000085)
            {
                Log(logListView, "Already log into SAP.");
                Log(logListView, "Please press Post button to continue.");
                return connectSQL;
            }
            else
            {
                Log(logListView, "Failed to connect to the SAP database. Connection Result: " + connectResult);
                return null;
            }
        }


        //make all function accept ListView as a parameter, that way when you use log, you can able to access the list view properties

        //Exec Invoice
        public static void Execute_Invoice_Script(SqlConnection connectSQL, ListView logListView, TextBox textboxView, ConnectSAP connectSAP)
        {

            var query = "exec SP_invoicePosting";
            var jsonResult = connectSQL.QueryFirstOrDefault<string>(query); // Retrieve the JSON string result

            if (!string.IsNullOrEmpty(jsonResult))
            {
                //Log(logListView, "Retrieved JSON:");
                //Log(logListView, jsonResult);

                // Deserialize the JSON string into a list of Invoice objects
                List<Invoice> invoices = JsonConvert.DeserializeObject<List<Invoice>>(jsonResult);

                // Accessing the deserialized data and logging
                foreach (var invoice in invoices)
                {
                    //insert SAP DI API here to put into sap.
                    //Log(logListView, $"CardCode: {invoice.CardCode}, NumAtCard: {invoice.NumAtCard}");
                    textboxView.Text = "Invoice " + invoice.CardCode;
                    Insert_Invoice_To_SAP(invoice, logListView, connectSAP, connectSQL);
                }
            }
            else
            {
                Log(logListView, "No data retrieved from the stored procedure.");
            }
            // Further processing with the retrieved invoices if needed
        }

        //Exec Commission
        public static void Execute_Commission_Script(SqlConnection connectSQL, ListView logListView, TextBox textboxView, ConnectSAP connectSAP)
        {

            var query = "exec SP_commissionPosting";
            // query should check whether the postflag is 1, if already 1 that means already post, and to prevent duplicate.

            var jsonResult = connectSQL.QueryFirstOrDefault<string>(query); // Retrieve the JSON string result

            if (!string.IsNullOrEmpty(jsonResult))
            {
                //Log(logListView, "Retrieved JSON:");
                //Log(logListView, jsonResult);

                // Deserialize the JSON string into a list of Invoice objects
                List<Commission> commissions = JsonConvert.DeserializeObject<List<Commission>>(jsonResult);

                // Accessing the deserialized data and logging
                foreach (var commission in commissions)
                {
                    //Log(logListView, $"Commission: CardCode: {commission.CardCode}, NumAtCard: {commission.NumAtCard}");
                    textboxView.Text = "Commission " + commission.CardCode;
                    Insert_Commission_To_SAP(commission, logListView, connectSAP, connectSQL);
                }
            }
            else
            {
                Log(logListView, "No data retrieved from the stored procedure.");
            }
            // Further processing with the retrieved invoices if needed
        }

        public static void Execute_RDCommission_Script(SqlConnection connectSQL, ListView logListView, TextBox textboxView, ConnectSAP connectSAP)
        {

            var query = "exec SP_commissionRedemptionPosting";
            // query should check whether the postflag is 1, if already 1 that means already post, and to prevent duplicate.

            var jsonResult = connectSQL.QueryFirstOrDefault<string>(query); // Retrieve the JSON string result

            if (!string.IsNullOrEmpty(jsonResult))
            {
                //Log(logListView, "Retrieved JSON:");
                //Log(logListView, jsonResult);

                // Deserialize the JSON string into a list of Invoice objects
                List<Commission> commissions = JsonConvert.DeserializeObject<List<Commission>>(jsonResult);

                // Accessing the deserialized data and logging
                foreach (var commission in commissions)
                {
                    //Log(logListView, $"Commission: CardCode: {commission.CardCode}, NumAtCard: {commission.NumAtCard}");
                    textboxView.Text = "RD Commission " + commission.CardCode;
                    Insert_RDCommission_To_SAP(commission, logListView, connectSAP, connectSQL);
                }
            }
            else
            {
                Log(logListView, "No data retrieved from the stored procedure.");
            }
            // Further processing with the retrieved invoices if needed
        }
        //Exec Credit Note
        public static void Execute_CreditNote_Script(SqlConnection connectSQL, ListView logListView, TextBox textboxView, ConnectSAP connectSAP)
        {

            var query = "exec SP_creditnotePosting"; //i exchange SP for testing
            var jsonResult = connectSQL.QueryFirstOrDefault<string>(query); // Retrieve the JSON string result

            if (!string.IsNullOrEmpty(jsonResult))
            {
                //Log(logListView, "Retrieved JSON:");
                //Log(logListView, jsonResult);

                // Deserialize the JSON string into a list of Invoice objects
                List<CreditNote> creditNotes = JsonConvert.DeserializeObject<List<CreditNote>>(jsonResult);

                // Accessing the deserialized data and logging
                foreach (var creditNote in creditNotes)
                {
                    Log(logListView, $"CardCode: {creditNote.CardCode}, NumAtCard: {creditNote.NumAtCard}");
                    textboxView.Text = "Credit Note " + creditNote.CardCode;
                    Insert_CreditNote_To_SAP(creditNote, logListView, connectSAP, connectSQL);
                }
            }
            else
            {
                Log(logListView, "No data retrieved from the stored procedure.");
            }

        }

        //Add invoice to SAP
        public static void Insert_Invoice_To_SAP(Invoice invoice, ListView logListView, ConnectSAP connectSAP, SqlConnection connectSQL)
        {
           
            Log(logListView, "Inserting " + $"Card Code: {invoice.CardCode}" + $" Document Number: {invoice.NumAtCard}" + " into SAP");
            Documents oINV = null;

            try
            {             
                oINV = (Documents)connectSAP.oCompany.GetBusinessObject(BoObjectTypes.oInvoices);
                
                oINV.CardCode = invoice.CardCode;
                oINV.Comments = invoice.Comments;
                oINV.DocDate = invoice.DocDate;
                oINV.DocDueDate = invoice.DocDueDate;  // disabled dates for testing as sample data exceed posting periods
                oINV.NumAtCard = invoice.NumAtCard;
                oINV.Project = invoice.Project;
                oINV.Series = invoice.Series;          
                oINV.TaxDate = invoice.TaxDate;

                //add lines
                foreach (var lines in invoice.Lines)
                {

                    oINV.Lines.ItemCode = lines.ItemCode;
                    oINV.Lines.UnitPrice = lines.UnitPrice;
                    oINV.Lines.ProjectCode = lines.ProjectCode;
                    oINV.Lines.VatGroup = lines.VatGroup;
                    oINV.Lines.Quantity = (double)lines.Quantity;

                    if (invoice.ExpensesLineTotal != 0)
                    {
                        oINV.Expenses.SetCurrentLine(0);
                        oINV.Expenses.ExpenseCode = invoice.ExpensesCode;
                        oINV.Expenses.LineTotal = invoice.ExpensesLineTotal; // value have to be negative
                        oINV.Expenses.VatGroup = invoice.ExpensesVatGroup;
                    }
                    oINV.Lines.Add();

                }
                    
                if (oINV.Add() != 0) //if 0 is success, else is fail
                {
                    string errmsg = $"{connectSAP.oCompany.GetLastErrorCode()} - {connectSAP.oCompany.GetLastErrorDescription()}";
                    Log(logListView, errmsg);
                }
                else
                {

                connectSAP.oCompany.GetNewObjectCode(out string docEntry);
                    Log(logListView,"Successfully added invoice. DocEntry: " + docEntry);
                    // run the post flag update script here, you need to pass a parameter though
                    Update_Invoice_PostFlag(connectSQL, logListView, invoice);
                }
                
            }
            finally
            {
                //Marshal.ReleaseComObject(connectSAP); //this function exclusive for windows 
                
                connectSAP = null; //need to clear the object, or else will cause lag
            }

        }
        //Add Commission to SAP
        public static void Insert_Commission_To_SAP(Commission commission, ListView logListView, ConnectSAP connectSAP, SqlConnection connectSQL)
        {
            
            Log(logListView, "Inserting " + $"CardCode: {commission.CardCode} " + $" Document Number: {commission.NumAtCard}" + " into SAP");
            Documents oCOM = null;

            try
            {
                oCOM = (Documents)connectSAP.oCompany.GetBusinessObject(BoObjectTypes.oCreditNotes);

                oCOM.CardCode = commission.CardCode;
                oCOM.Comments = commission.Comments;
                oCOM.DocDate = commission.DocDate;
                oCOM.DocDueDate = commission.DocDueDate;
                oCOM.DocType = commission.DocType;
                oCOM.HandWritten = commission.Handwritten;
                oCOM.NumAtCard = commission.NumAtCard;
                oCOM.Project = commission.Project;
                oCOM.Series = commission.Series;
                oCOM.TaxDate = commission.TaxDate;

                //add lines
                //int line = -1;
                foreach (var lines in commission.Lines)
                {
                    //line++;
                    //if (line > 0) 
                    //    oCOM.Lines.Add();
                    //oCOM.Lines.SetCurrentLine(line);
                    oCOM.Lines.ItemDescription = lines.ItemDescription;
                    oCOM.Lines.UnitPrice = lines.UnitPrice;
                    oCOM.Lines.AccountCode = lines.AccountCode;  //SAP requires Account code insert to lines
                    if (!string.IsNullOrEmpty(lines.ProjectCode))
                        oCOM.Lines.ProjectCode = lines.ProjectCode;
                    oCOM.Lines.VatGroup = lines.VatGroup;
                    //oCOM.Lines.Quantity = lines.Quantity;  //quantity not require in service document
                    oCOM.Lines.UserFields.Fields.Item("U_FRef").Value = lines.U_FRef; //user defined fields
                    oCOM.Lines.Add();

                }

                if (oCOM.Add() != 0) //if 0 is success, else is fail
                {

                    string errmsg = $"{connectSAP.oCompany.GetLastErrorCode()} - {connectSAP.oCompany.GetLastErrorDescription()}";
                    Log(logListView, errmsg);
                }
                else
                {

                    connectSAP.oCompany.GetNewObjectCode(out string docEntry);
                    Log(logListView, "Successfully added RD commission. DocEntry: " + docEntry);
                    Update_Commission_PostFlag(connectSQL, logListView, commission);
                }

            }
            finally
            {
                //Marshal.ReleaseComObject(connectSAP); //this function exclusive for windows 
                connectSAP = null; //need to clear the object, or else will cause lag
            }

        }
        public static void Insert_RDCommission_To_SAP(Commission commission, ListView logListView, ConnectSAP connectSAP, SqlConnection connectSQL)
        {

            Log(logListView, "Inserting " + $"CardCode: {commission.CardCode} " + $" Document Number: {commission.NumAtCard}" + " into SAP");
            Documents oCOM = null;

            try
            {
                oCOM = (Documents)connectSAP.oCompany.GetBusinessObject(BoObjectTypes.oCreditNotes);

                oCOM.CardCode = commission.CardCode;
                oCOM.Comments = commission.Comments;
                oCOM.DocDate = commission.DocDate;
                oCOM.DocDueDate = commission.DocDueDate;
                oCOM.TaxDate = commission.TaxDate;
                oCOM.DocType = commission.DocType;
                oCOM.HandWritten = commission.Handwritten;
                oCOM.NumAtCard = commission.NumAtCard;
                oCOM.Series = commission.Series;
                if (!string.IsNullOrEmpty(commission.Project))
                    oCOM.Project = commission.Project;
                if (!string.IsNullOrEmpty(commission.U_EIV_Consolidate))
                    oCOM.UserFields.Fields.Item("U_EIV_Consolidate").Value = commission.U_EIV_Consolidate;
                if (!string.IsNullOrEmpty(commission.U_EIV_InvoiceType))
                    oCOM.UserFields.Fields.Item("U_EIV_InvoiceType").Value = commission.U_EIV_InvoiceType;
                if (!string.IsNullOrEmpty(commission.U_EIV_FreqSync))
                    oCOM.UserFields.Fields.Item("U_EIV_FreqSync").Value = commission.U_EIV_FreqSync;
                if (!string.IsNullOrEmpty(commission.U_EIV_OriRefNum))
                    oCOM.UserFields.Fields.Item("U_EIV_OriRefNum").Value = commission.U_EIV_OriRefNum;
                if (!string.IsNullOrEmpty(commission.U_EIV_OriCode))
                    oCOM.UserFields.Fields.Item("U_EIV_OriCode").Value = commission.U_EIV_OriCode;
                if (!string.IsNullOrEmpty(commission.U_EIV_SupplierMSIC))
                    oCOM.UserFields.Fields.Item("U_EIV_SupplierMSIC").Value = commission.U_EIV_SupplierMSIC;
                if (!string.IsNullOrEmpty(commission.U_EIV_SupplierBActivity))
                    oCOM.UserFields.Fields.Item("U_EIV_SupplierBActivity").Value = commission.U_EIV_SupplierBActivity;
                //add lines
                //int line = -1;
                foreach (var lines in commission.Lines)
                {
                    //line++;
                    //if (line > 0) 
                    //    oCOM.Lines.Add();
                    //oCOM.Lines.SetCurrentLine(line);
                    oCOM.Lines.ItemDescription = lines.ItemDescription;
                    oCOM.Lines.UnitPrice = lines.UnitPrice;
                    oCOM.Lines.AccountCode = lines.AccountCode;  //SAP requires Account code insert to lines
                    if (!string.IsNullOrEmpty(lines.ProjectCode))
                        oCOM.Lines.ProjectCode = lines.ProjectCode;
                    if (!string.IsNullOrEmpty(lines.VatGroup))
                        oCOM.Lines.VatGroup = lines.VatGroup;
                    //oCOM.Lines.Quantity = lines.Quantity;  //quantity not require in service document
                    if (!string.IsNullOrEmpty(lines.U_FRef))
                        oCOM.Lines.UserFields.Fields.Item("U_FRef").Value = lines.U_FRef; //user defined fields
                    if (!string.IsNullOrEmpty(lines.U_EIV_Classification))
                        oCOM.Lines.UserFields.Fields.Item("U_EIV_Classification").Value = lines.U_EIV_Classification;
                    if (!string.IsNullOrEmpty(lines.U_EIV_Measure))
                        oCOM.Lines.UserFields.Fields.Item("U_EIV_Measure").Value = lines.U_EIV_Measure;
                    oCOM.Lines.Add();

                }

                if (oCOM.Add() != 0) //if 0 is success, else is fail
                {

                    string errmsg = $"{connectSAP.oCompany.GetLastErrorCode()} - {connectSAP.oCompany.GetLastErrorDescription()}";
                    Log(logListView, errmsg);
                }
                else
                {

                    connectSAP.oCompany.GetNewObjectCode(out string docEntry);
                    Log(logListView, "Successfully added RD commission. DocEntry: " + docEntry);
                    Update_RDCommission_PostFlag(connectSQL, logListView, commission);
                }

            }
            finally
            {
                //Marshal.ReleaseComObject(connectSAP); //this function exclusive for windows 
                connectSAP = null; //need to clear the object, or else will cause lag
            }

        }

        //Add CreditNote to SAP
        public static void Insert_CreditNote_To_SAP(CreditNote creditNote, ListView logListView, ConnectSAP connectSAP, SqlConnection connectSQL)
        {

            Log(logListView, "Inserting " + $"CardCode: {creditNote.CardCode} " + $" Document Number: {creditNote.NumAtCard}" + " into SAP");
            Documents oCOM = null;

            try
            {
                oCOM = (Documents)connectSAP.oCompany.GetBusinessObject(BoObjectTypes.oCreditNotes);

                oCOM.CardCode = creditNote.CardCode;
                oCOM.Comments = creditNote.Comments;
                oCOM.DocDate = creditNote.DocDate;
                oCOM.DocDueDate = creditNote.DocDueDate;
                oCOM.NumAtCard = creditNote.NumAtCard;
                oCOM.Project = creditNote.Project;
                oCOM.Series = creditNote.Series;
                oCOM.TaxDate = creditNote.TaxDate;

                //add lines
                foreach (var lines in creditNote.Lines)
                {

                    oCOM.Lines.ItemCode = lines.ItemCode;
                    oCOM.Lines.UnitPrice = lines.UnitPrice;
                    oCOM.Lines.ProjectCode = lines.ProjectCode;
                    oCOM.Lines.VatGroup = lines.VatGroup; 
                    oCOM.Lines.Quantity = lines.Quantity;
                    oCOM.Lines.UserFields.Fields.Item("U_FRef").Value = "0";//lines.U_FRef; //user defined fields

                    if (creditNote.ExpensesLineTotal != 0)
                    {
                        oCOM.Expenses.SetCurrentLine(0);
                        oCOM.Expenses.ExpenseCode = creditNote.ExpensesCode;
                        oCOM.Expenses.LineTotal = creditNote.ExpensesLineTotal; // value have to be negative
                        oCOM.Expenses.VatGroup = creditNote.ExpensesVatGroup;
                    }
                    oCOM.Lines.Add();

                }

                if (oCOM.Add() != 0) //if 0 is success, else is fail
                {

                    string errmsg = $"{connectSAP.oCompany.GetLastErrorCode()} - {connectSAP.oCompany.GetLastErrorDescription()}";
                    Log(logListView, errmsg);
                }
                else
                {

                    connectSAP.oCompany.GetNewObjectCode(out string docEntry);
                    Log(logListView, "Successfully added credit note. DocEntry: " + docEntry);
                    Update_CreditNote_PostFlag(connectSQL, logListView, creditNote);
                }

            }
            finally
            {
                //Marshal.ReleaseComObject(connectSAP); //this function exclusive for windows 
                connectSAP = null; //need to clear the object, or else will cause lag
            }

        }

        // get some sample credit note rows using this query below, this is for testing only
        //        select* from creditnote order by sk_hdr desc
        //update creditnote set postflag = 0 where sk_hdr >= 213

        //credit note dont have UREF
        //commission have UREF

        //set post flag to 1 after posting //updated kw script to retrieve SKHDR
        public static void Update_Invoice_PostFlag(SqlConnection connectSQL, ListView logListView, Invoice invoice)
        {
            
            var query = "update invoice set postflag = 1 where postflag = 0 and chkflag = 1 and sk_hdr = @SKHDR"; //i exchange SP for testing
            connectSQL.Query(query, new { SKHDR = invoice.sk_hdr });
            Log(logListView, "Invoice postflag sk_hdr: " + invoice.sk_hdr + " updated.");
        }

        public static void Update_Commission_PostFlag(SqlConnection connectSQL, ListView logListView, Commission commission)
        {
            
            var query = "update comm_iv set postflag = 1 where postflag = 0 and chkflag = 1 and sk_hdr = @SKHDR"; //i exchange SP for testing
            connectSQL.Query(query, new { SKHDR = commission.sk_hdr });
            Log(logListView, "Commission postflag sk_hdr: " + commission.sk_hdr + " updated.");
        }
        public static void Update_RDCommission_PostFlag(SqlConnection connectSQL, ListView logListView, Commission commission)
        {

            var query = "update comm_rd set postflag = 1 where postflag = 0 and chkflag = 1 and sk_hdr = @SKHDR"; //i exchange SP for testing
            connectSQL.Query(query, new { SKHDR = commission.sk_hdr });
            Log(logListView, "RD Commission postflag sk_hdr: " + commission.sk_hdr + " updated.");
        }

        public static void Update_CreditNote_PostFlag(SqlConnection connectSQL, ListView logListView, CreditNote creditNote)
        {
            
            var query = "update creditnote set postflag = 1 where postflag = 0 and chkflag = 1 and sk_hdr = @SKHDR"; //i exchange SP for testing
            connectSQL.Query(query, new { SKHDR = creditNote.sk_hdr });
            Log(logListView, "Credit Note postflag sk_hdr: " + creditNote.sk_hdr + " updated.");
        }

    }
}
