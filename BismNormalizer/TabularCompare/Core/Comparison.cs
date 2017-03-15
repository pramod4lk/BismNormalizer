﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;
using Microsoft.AnalysisServices;
using System.Xml;

namespace BismNormalizer.TabularCompare.Core
{
    /// <summary>
    /// Represents a comparison of two SSAS tabular models. This class is extended by BismNormalizer.TabularCompare.MultidimensionalMetadata.Comparison and BismNormalizer.TabularCompare.TabularMetadata.Comparison depending on SSAS compatibility level.
    /// </summary>
    public abstract class Comparison : IDisposable
    {
        #region Protetced/Private Members

        protected List<ComparisonObject> _comparisonObjects;
        protected ComparisonInfo _comparisonInfo;
        protected int _comparisonObjectCount = 0;
        private int _compatibilityLevel;

        #endregion

        #region Properties

        /// <summary>
        /// Collection of ComparisonObject instances.
        /// </summary>
        public List<ComparisonObject> ComparisonObjects
        {
            get { return _comparisonObjects; }
            set { _comparisonObjects = value; }
        }

        /// <summary>
        /// Compatibility level of the SSAS tabular models for this comparison.
        /// </summary>
        public int CompatibilityLevel => _compatibilityLevel;

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a validation message is surfaced, either warning or informational.
        /// </summary>
        public event EventHandler<ValidationMessageEventArgs> ValidationMessage;

        /// <summary>
        /// Occurs when all messages for a validation are done, and need to dynamically resize the headers.
        /// </summary>
        public event EventHandler<EventArgs> ResizeValidationHeaders;

        /// <summary>
        /// Invokes the ValidationMessage event.
        /// </summary>
        /// <param name="e">ValidationMessageEventArgs object.</param>
        public virtual void OnValidationMessage(ValidationMessageEventArgs e) => ValidationMessage?.Invoke(this, e);

        /// <summary>
        /// Invokes the ResizeValidationHeaders event.
        /// </summary>
        /// <param name="e">EventArgs object.</param>
        public virtual void OnResizeValidationHeaders(EventArgs e) => ResizeValidationHeaders?.Invoke(this, e);

        /// <summary>
        /// Occurs during database deployment when a password is required for an impersonated account.
        /// </summary>
        public event EventHandler<PasswordPromptEventArgs> PasswordPrompt;

        /// <summary>
        /// Invokes the PasswordPrompt event.
        /// </summary>
        /// <param name="e">ValidationMessageEventArgs object.</param>
        public virtual void OnPasswordPrompt(PasswordPromptEventArgs e) => PasswordPrompt?.Invoke(this, e);

        /// <summary>
        /// Occurs when a database is ready for deployment.
        /// </summary>
        public event EventHandler<DatabaseDeploymentEventArgs> DatabaseDeployment;

        /// <summary>
        /// Invokes the DatabaseDeployment event.
        /// </summary>
        /// <param name="e">DatabaseDeploymentEventArgs object.</param>
        public virtual void OnDatabaseDeployment(DatabaseDeploymentEventArgs e) => DatabaseDeployment?.Invoke(this, e);

        /// <summary>
        /// Occurs when a deployment status message is surfaced.
        /// </summary>
        public event EventHandler<DeploymentMessageEventArgs> DeploymentMessage;

        /// <summary>
        /// Invokes the DeploymentMessage event.
        /// </summary>
        /// <param name="e">DeploymentMessageEventArgs object.</param>
        public virtual void OnDeploymentMessage(DeploymentMessageEventArgs e) => DeploymentMessage?.Invoke(this, e);

        /// <summary>
        /// Occurs when a database deployment is complete.
        /// </summary>
        public event EventHandler<DeploymentCompleteEventArgs> DeploymentComplete;

        /// <summary>
        /// Invokes the DeploymentComplete event.
        /// </summary>
        /// <param name="e">DeploymentCompleteEventArgs object.</param>
        public virtual void OnDeploymentComplete(DeploymentCompleteEventArgs e) => DeploymentComplete?.Invoke(this, e);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the Comparison class using a ComparisonInfo object.
        /// </summary>
        /// <param name="comparisonInfo">ComparisonInfo object typically deserialized from a BSMN file.</param>
        public Comparison(ComparisonInfo comparisonInfo)
        {
            _comparisonObjects = new List<ComparisonObject>();
            _comparisonInfo = comparisonInfo;
            //Supported compatibility level - with matching source/target compatibility levels - has already been validated at this point, so can safely use SourceCompatibilityLevel
            _compatibilityLevel = comparisonInfo.SourceCompatibilityLevel;
        }

        #endregion

        #region Abstract Methods

        /// <summary>
        /// Connect to source and target tabular models, and instantiate their properties.
        /// </summary>
        public abstract void Connect();

        /// <summary>
        /// Disconnect from source and target tabular models.
        /// </summary>
        public abstract void Disconnect();

        /// <summary>
        /// Validate selection of actions to perform on target tabular model. Warnings and informational messages are provided by invoking ShowStatusMessageCallBack.
        /// </summary>
        public abstract void ValidateSelection();

        /// <summary>
        /// Update target tabular model with changes defined by actions in ComparisonObject instances.
        /// </summary>
        /// <returns>Flag to indicate whether update was successful.</returns>
        public abstract bool Update();

        /// <summary>
        /// Gets a collection of ProcessingTable objects depending on Process Affected Tables option.
        /// </summary>
        /// <returns>Collection of ProcessingTable objects.</returns>
        public abstract ProcessingTableCollection GetTablesToProcess();

        /// <summary>
        /// Deploy database to target server and perform processing if required.
        /// </summary>
        /// <param name="tablesToProcess"></param>
        public abstract void DatabaseDeployAndProcess(ProcessingTableCollection tablesToProcess);

        /// <summary>
        /// Stop processing of deployed database.
        /// </summary>
        public abstract void StopProcessing();

        /// <summary>
        /// Generate script of target database including changes.
        /// </summary>
        /// <returns>Script.</returns>
        public abstract string ScriptDatabase();

        /// <summary>
        /// Compare source and target tabular models.
        /// </summary>
        public abstract void CompareTabularModels();

        #endregion

        /// <summary>
        /// Finds ComparisonObject matching search criteria.
        /// </summary>
        /// <param name="sourceObjectName"></param>
        /// <param name="sourceObjectId"></param>
        /// <param name="targetObjectName"></param>
        /// <param name="targetObjectId"></param>
        /// <param name="objType"></param>
        /// <returns>ComparisonObject matching search criteria. If none found, null is returned.</returns>
        public ComparisonObject FindComparisonObjectByObjectInternalNames(string sourceObjectName, string sourceObjectId, string targetObjectName, string targetObjectId, ComparisonObjectType objType)
        {
            foreach (ComparisonObject comparisonObject in _comparisonObjects)
            {
                ComparisonObject matchedComparisonObj;
                if (CheckComparisonObject(comparisonObject, sourceObjectName, sourceObjectId, targetObjectName, targetObjectId, objType, out matchedComparisonObj))
                {
                    return matchedComparisonObj;
                }
            }
            // if didn't find a match, return null
            return null;
        }

        private bool CheckComparisonObject(ComparisonObject comparisonObject, string sourceObjectName, string sourceObjectId, string targetObjectName, string targetObjectId, ComparisonObjectType objType, out ComparisonObject matchedComparisonObj)
        {
            if (comparisonObject.SourceObjectName == sourceObjectName && comparisonObject.SourceObjectInternalName == sourceObjectId && comparisonObject.TargetObjectName == targetObjectName && comparisonObject.TargetObjectInternalName == targetObjectId && comparisonObject.ComparisonObjectType == objType)
            {
                matchedComparisonObj = comparisonObject;
                return true;
            }
            foreach (ComparisonObject childComparisonObject in comparisonObject.ChildComparisonObjects)
            {
                if (CheckComparisonObject(childComparisonObject, sourceObjectName, sourceObjectId, targetObjectName, targetObjectId, objType, out matchedComparisonObj))
                {
                    if (matchedComparisonObj == null)
                        matchedComparisonObj = childComparisonObject;
                    return true;
                }
            }
            // if didn't find a match, return null
            matchedComparisonObj = null;
            return false;
        }

        /// <summary>
        /// Generate Excel report of differences.
        /// </summary>
        /// <param name="progBar"></param>
        public void ReportDifferences(ProgressBar progBar)
        {
            try
            {
                progBar.Maximum = _comparisonObjectCount;
                progBar.Value = 0;

                Excel.Application App = new Excel.Application();
                Excel.Workbook Wb = App.Workbooks.Add();
                //Wb.Sheets[2].Delete();
                //Wb.Sheets[1].Delete();
                Excel.Worksheet Ws = default(Excel.Worksheet);
                Ws = Wb.ActiveSheet;
                Ws.Name = "Bism Normalizer Report";
                int row = 1, lastConnectionRow = -1, lastTableRow = -1;

                // set up headers
                Ws.Cells[row, 1].Value = "Type";
                Ws.Columns[1].ColumnWidth = 20;
                Ws.Cells[row, 2].Value = "Source Object Name";
                Ws.Columns[2].ColumnWidth = 41;
                Ws.Cells[row, 3].Value = "Status";
                Ws.Columns[3].ColumnWidth = 18;
                Ws.Cells[row, 4].Value = "Target Object Name";
                Ws.Columns[4].ColumnWidth = 41;
                Ws.Range["A1:D1"].Select();
                Ws.Application.Selection.Font.Bold = true;

                //set up grouping
                Ws.Outline.AutomaticStyles = false;
                Ws.Outline.SummaryRow = (Excel.XlSummaryRow)Excel.Constants.xlAbove;
                Ws.Outline.SummaryColumn = (Excel.XlSummaryColumn)Excel.Constants.xlLeft;

                foreach (ComparisonObject comparisonObject in _comparisonObjects)
                {
                    PopulateExcelRow(Ws, ref row, ref lastConnectionRow, ref lastTableRow, comparisonObject, progBar);
                }

                // do we need to close the last groups?
                if (lastTableRow < row && lastTableRow != -1)
                {
                    Ws.Application.Rows[Convert.ToString(lastTableRow + 1) + ":" + Convert.ToString(row)].Select();
                    Ws.Application.Selection.Rows.Group();
                }
                if (lastConnectionRow < row && lastConnectionRow != -1)
                {
                    Ws.Application.Rows[Convert.ToString(lastConnectionRow + 1) + ":" + Convert.ToString(row)].Select();
                    Ws.Application.Selection.Rows.Group();
                }

                Ws.Cells[1, 1].Select();
                App.Visible = true;
                progBar.Value = 0;
            }
            catch (System.Runtime.InteropServices.COMException exc)
            {
                throw new System.Runtime.InteropServices.COMException("Unable to create Excel report. Please check Excel is installed.", exc);
            }
        }

        /// <summary>
        /// Refresh SkipSelections property.
        /// </summary>
        public void RefreshSkipSelectionsFromComparisonObjects()
        {
            _comparisonInfo.SkipSelections.Clear();

            foreach (ComparisonObject comparisonObject in this.ComparisonObjects)
            {
                RefreshSkipSelectionsFromChildComparisonObjects(comparisonObject);
            }
        }

        private void RefreshSkipSelectionsFromChildComparisonObjects(ComparisonObject comparisonObject)
        {
            if (comparisonObject.Status != ComparisonObjectStatus.SameDefinition && comparisonObject.UpdateAction == UpdateAction.Skip && !_comparisonInfo.SkipSelections.Contains(comparisonObject))
            {
                _comparisonInfo.SkipSelections.Add(new SkipSelection(comparisonObject));
            }

            foreach (ComparisonObject childComparisonObject in comparisonObject.ChildComparisonObjects)
            {
                RefreshSkipSelectionsFromChildComparisonObjects(childComparisonObject);
            }
        }

        /// <summary>
        /// Refresh ComparisonObjects property.
        /// </summary>
        public void RefreshComparisonObjectsFromSkipSelections()
        {
            foreach (ComparisonObject comparisonObject in this.ComparisonObjects)
            {
                RefreshChildComparisonObjectsFromSkipSelections(comparisonObject);
            }
        }

        private void RefreshChildComparisonObjectsFromSkipSelections(ComparisonObject comparisonObject)
        {
            if (comparisonObject.Status != ComparisonObjectStatus.SameDefinition)
            {
                foreach (SkipSelection skipSelection in _comparisonInfo.SkipSelections)
                {
                    if (comparisonObject.Status == skipSelection.Status && comparisonObject.ComparisonObjectType == skipSelection.ComparisonObjectType && (skipSelection.Status == ComparisonObjectStatus.MissingInSource || comparisonObject.SourceObjectInternalName == skipSelection.SourceObjectInternalName) && (skipSelection.Status == ComparisonObjectStatus.MissingInTarget || comparisonObject.TargetObjectInternalName == skipSelection.TargetObjectInternalName))
                    {
                        comparisonObject.UpdateAction = UpdateAction.Skip;
                        break;
                    }
                }
            }

            foreach (ComparisonObject childComparisonObject in comparisonObject.ChildComparisonObjects)
            {
                RefreshChildComparisonObjectsFromSkipSelections(childComparisonObject);
            }
        }

        private void PopulateExcelRow(Excel.Worksheet Ws, ref int row, ref int lastConnectionRow, ref int lastTableRow, ComparisonObject comparisonObject, ProgressBar progBar)
        {
            progBar.PerformStep();
            row += 1;

            // Close out groups if necessary
            if (comparisonObject.ComparisonObjectType == ComparisonObjectType.Connection || comparisonObject.ComparisonObjectType == ComparisonObjectType.Table || comparisonObject.ComparisonObjectType == ComparisonObjectType.Perspective || comparisonObject.ComparisonObjectType == ComparisonObjectType.Culture || comparisonObject.ComparisonObjectType == ComparisonObjectType.Role || comparisonObject.ComparisonObjectType == ComparisonObjectType.Action) //treat perspectives/cultures/roles like connections for purpose of grouping
            {
                // do we need to close a table group?
                if (lastTableRow + 1 < row && lastTableRow != -1)
                {
                    Ws.Application.Rows[Convert.ToString(lastTableRow + 1) + ":" + Convert.ToString(row - 1)].Select();
                    Ws.Application.Selection.Rows.Group();
                }
                lastTableRow = row;

                if (comparisonObject.ComparisonObjectType == ComparisonObjectType.Connection || comparisonObject.ComparisonObjectType == ComparisonObjectType.Perspective || comparisonObject.ComparisonObjectType == ComparisonObjectType.Culture || comparisonObject.ComparisonObjectType == ComparisonObjectType.Role || comparisonObject.ComparisonObjectType == ComparisonObjectType.Action) //treat perspectives/roles like connections for purpose of grouping
                {
                    // do we need to close a connection group?
                    if (lastConnectionRow + 1 < row && lastConnectionRow != -1)
                    {
                        Ws.Application.Rows[Convert.ToString(lastConnectionRow + 1) + ":" + Convert.ToString(row - 1)].Select();
                        Ws.Application.Selection.Rows.Group();
                    }
                    lastConnectionRow = row;
                }
            }

            //Type column
            switch (comparisonObject.ComparisonObjectType)
            {
                case ComparisonObjectType.Connection:
                    Ws.Cells[row, 1].Value = "Connection";
                    break;
                case ComparisonObjectType.Table:
                    Ws.Cells[row, 1].Value = "Table";
                    Ws.Cells[row, 1].InsertIndent(3);
                    Ws.Cells[row, 2].InsertIndent(3);
                    Ws.Cells[row, 4].InsertIndent(3);
                    break;
                case ComparisonObjectType.Relationship:
                    Ws.Cells[row, 1].Value = "Relationship";
                    Ws.Cells[row, 1].InsertIndent(6);
                    Ws.Cells[row, 2].InsertIndent(3);
                    Ws.Cells[row, 4].InsertIndent(3);
                    break;
                case ComparisonObjectType.Measure:
                    Ws.Cells[row, 1].Value = "Measure";
                    Ws.Cells[row, 1].InsertIndent(6);
                    Ws.Cells[row, 2].InsertIndent(3);
                    Ws.Cells[row, 4].InsertIndent(3);
                    break;
                case ComparisonObjectType.Kpi:
                    Ws.Cells[row, 1].Value = "KPI";
                    Ws.Cells[row, 1].InsertIndent(6);
                    Ws.Cells[row, 2].InsertIndent(3);
                    Ws.Cells[row, 4].InsertIndent(3);
                    break;
                case ComparisonObjectType.Perspective:
                    Ws.Cells[row, 1].Value = "Perspective";
                    break;
                case ComparisonObjectType.Culture:
                    Ws.Cells[row, 1].Value = "Culture";
                    break;
                case ComparisonObjectType.Role:
                    Ws.Cells[row, 1].Value = "Role";
                    break;
                case ComparisonObjectType.Action:
                    Ws.Cells[row, 1].Value = "Action";
                    break;
                default:
                    Ws.Cells[row, 1].Value = comparisonObject.ComparisonObjectType.ToString();
                    break;
            }

            //Source Obj Name column
            if (comparisonObject.SourceObjectName != null && comparisonObject.SourceObjectName != "")
            {
                Ws.Cells[row, 2].Value = comparisonObject.SourceObjectName;
                //source obj def
                Ws.Cells[row, 2].AddComment();
                Ws.Cells[row, 2].Comment.Visible = false;
                if (comparisonObject.SourceObjectDefinition != null && comparisonObject.SourceObjectDefinition != "")
                {
                    Ws.Cells[row, 2].Comment.Text("Object Definition:\n\n" + comparisonObject.SourceObjectDefinition);
                    Ws.Cells[row, 2].Comment.Shape.TextFrame.Characters.Font.Bold = false;
                    Ws.Cells[row, 2].Comment.Shape.Width = 200;
                    Ws.Cells[row, 2].Comment.Shape.Height = 250;
                }
            }
            else
            {
                Ws.Cells[row, 2].Interior.Pattern = Excel.Constants.xlSolid;
                Ws.Cells[row, 2].Interior.PatternColorIndex = Excel.Constants.xlAutomatic;
                Ws.Cells[row, 2].Interior.ThemeColor = Excel.XlThemeColor.xlThemeColorDark1;
                Ws.Cells[row, 2].Interior.TintAndShade = -0.149998474074526;
                Ws.Cells[row, 2].Interior.PatternTintAndShade = 0;
            }

            //status
            switch (comparisonObject.Status)
            {
                case ComparisonObjectStatus.SameDefinition:
                    Ws.Cells[row, 3].Value = "Same Definition";
                    break;
                case ComparisonObjectStatus.DifferentDefinitions:
                    Ws.Cells[row, 3].Value = "Different Definitions";
                    break;
                case ComparisonObjectStatus.MissingInTarget:
                    Ws.Cells[row, 3].Value = "Missing in Target";
                    break;
                case ComparisonObjectStatus.MissingInSource:
                    Ws.Cells[row, 3].Value = "Missing in Source";
                    break;
                default:
                    Ws.Cells[row, 3].Value = comparisonObject.Status.ToString();
                    break;
            }

            //Target Obj Name column
            if (comparisonObject.TargetObjectName != null && comparisonObject.TargetObjectName != "")
            {
                Ws.Cells[row, 4].Value = comparisonObject.TargetObjectName;
                //Target obj def
                Ws.Cells[row, 4].AddComment();
                Ws.Cells[row, 4].Comment.Visible = false;
                if (comparisonObject.TargetObjectDefinition != null && comparisonObject.TargetObjectDefinition != "")
                {
                    Ws.Cells[row, 4].Comment.Text("Object Definition:\n\n" + comparisonObject.TargetObjectDefinition);
                    Ws.Cells[row, 4].Comment.Shape.TextFrame.Characters.Font.Bold = false;
                    Ws.Cells[row, 4].Comment.Shape.Width = 200;
                    Ws.Cells[row, 4].Comment.Shape.Height = 250;
                }
            }
            else
            {
                Ws.Cells[row, 4].Interior.Pattern = Excel.Constants.xlSolid;
                Ws.Cells[row, 4].Interior.PatternColorIndex = Excel.Constants.xlAutomatic;
                Ws.Cells[row, 4].Interior.ThemeColor = Excel.XlThemeColor.xlThemeColorDark1;
                Ws.Cells[row, 4].Interior.TintAndShade = -0.149998474074526;
                Ws.Cells[row, 4].Interior.PatternTintAndShade = 0;
            }

            foreach (ComparisonObject childComparisonObject in comparisonObject.ChildComparisonObjects)
            {
                PopulateExcelRow(Ws, ref row, ref lastConnectionRow, ref lastTableRow, childComparisonObject, progBar);
            }
        }

        #region Helper functions to execute XMLA

        /// <summary>
        /// Finds row count for a table to display after processing.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="tableName"></param>
        /// <param name="databaseName"></param>
        /// <returns>Row count.</returns>
        public static int FindRowCount(Microsoft.AnalysisServices.Core.Server server, string tableName, string databaseName)
        {
            string dax = String.Format("EVALUATE ROW( \"RowCount\", COUNTROWS('{0}'))", tableName);
            XmlNodeList rows = ExecuteDaxXmlaCommand(server, dax, databaseName);

            foreach (XmlNode row in rows)
            {
                XmlNode rowCountNode = null;

                foreach (XmlNode childNode in row.ChildNodes)
                {
                    if (childNode.Name.Contains("RowCount"))
                    {
                        rowCountNode = childNode;
                    }
                }

                int result;
                if (rowCountNode != null && int.TryParse(rowCountNode.InnerText, out result))
                {
                    return result;
                }
            }

            return 0;
        }

        /// <summary>
        /// Executes DAX command.
        /// </summary>
        /// <param name="server"></param>
        /// <param name="commandStatement"></param>
        /// <param name="databaseName"></param>
        /// <returns>XmlNodeList results.</returns>
        public static XmlNodeList ExecuteDaxXmlaCommand(Microsoft.AnalysisServices.Core.Server server, string commandStatement, string databaseName)
        {
            XmlWriter xmlWriter = server.StartXmlaRequest(XmlaRequestType.Undefined);
            WriteSoapEnvelopeWithDaxCommandStatement(xmlWriter, server.SessionID, commandStatement, databaseName);
            System.Xml.XmlReader xmlReader = server.EndXmlaRequest();
            xmlReader.MoveToContent();
            string fullEnvelopeResponseFromServer = xmlReader.ReadOuterXml();
            xmlReader.Close();

            XmlDocument documentResponse = new XmlDocument();
            documentResponse.LoadXml(fullEnvelopeResponseFromServer);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(documentResponse.NameTable);
            nsmgr.AddNamespace("myns1", "urn:schemas-microsoft-com:xml-analysis");
            nsmgr.AddNamespace("myns2", "urn:schemas-microsoft-com:xml-analysis:rowset");
            XmlNodeList rows = documentResponse.SelectNodes("//myns1:ExecuteResponse/myns1:return/myns2:root/myns2:row", nsmgr);
            return rows;
        }

        private static void WriteSoapEnvelopeWithDaxCommandStatement(XmlWriter xmlWriter, string sessionId, string commandStatement, string databaseName)
        {
            //--------------------------------------------------------------------------------
            // This is a sample of the XMLA request we'll write:
            //
            //<Envelope xmlns=""http://schemas.xmlsoap.org/soap/envelope/"">
            //   <Header>
            //     <Session soap:mustUnderstand="1" SessionId="THE SESSION ID HERE" xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" xmlns="urn:schemas-microsoft-com:xml-analysis" />
            //   </Header>
            //	<Body>
            //		<Execute xmlns=""urn:schemas-microsoft-com:xml-analysis"">
            //			<Command>
            //				<Statement>
            //					EVALUATE ROW( "Result", COUNTROWS('FactInternetSales'))
            //				</Statement>
            //			</Command>
            //			<Properties>
            //				<PropertyList>
            //					<Catalog>Tabular1200 V2</Catalog>
            //					<Format>Tabular</Format> 
            //					<Content>Data</Content> 
            //				</PropertyList>
            //			</Properties>
            //		</Execute>
            //	</Body>
            //</Envelope>
            //--------------------------------------------------------------------------------
            xmlWriter.WriteStartElement("Envelope", "http://schemas.xmlsoap.org/soap/envelope/");
            xmlWriter.WriteStartElement("Header");
            if (sessionId != null)
            {
                xmlWriter.WriteStartElement("Session", "urn:schemas-microsoft-com:xml-analysis");
                xmlWriter.WriteAttributeString("soap", "mustUnderstand", "http://schemas.xmlsoap.org/soap/envelope/", "1");
                xmlWriter.WriteAttributeString("SessionId", sessionId);
                xmlWriter.WriteEndElement(); // </Session>
            }
            xmlWriter.WriteEndElement(); // </Header>
            xmlWriter.WriteStartElement("Body");
            xmlWriter.WriteStartElement("Execute", "urn:schemas-microsoft-com:xml-analysis");
            xmlWriter.WriteStartElement("Command");
            xmlWriter.WriteElementString("Statement", commandStatement);
            xmlWriter.WriteEndElement(); // </Command>
            xmlWriter.WriteStartElement("Properties");
            xmlWriter.WriteStartElement("PropertyList");
            xmlWriter.WriteElementString("Catalog", databaseName);
            xmlWriter.WriteElementString("Format", "Tabular");
            xmlWriter.WriteElementString("Content", "Data");
            xmlWriter.WriteEndElement(); // </PropertyList>
            xmlWriter.WriteEndElement(); // </Properties>
            xmlWriter.WriteEndElement(); // </Execute>
            xmlWriter.WriteEndElement(); // </Body>
            xmlWriter.WriteEndElement(); // </Envelope>
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);
    }
}
