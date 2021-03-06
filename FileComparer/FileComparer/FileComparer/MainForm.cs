﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using System.Linq;
using HL.FileComparer.Dialogs;
using HL.FileComparer.Utilities;
using System.IO;

namespace HL.FileComparer
{
    public partial class MainForm : Form
    {                      
        private Dictionary<string, List<FileHashPair>> possibleMatches;        
        private BackgroundWorker worker;
        private SearchPattern selectedPattern;
        private bool workerWasCancelled;

        public MainForm()
        {
            InitializeComponent();
            
            possibleMatches = new Dictionary<string, List<FileHashPair>>();

            worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.WorkerSupportsCancellation = true;           
            worker.DoWork += worker_DoWork;
            worker.ProgressChanged += worker_ProgressChanged;
            worker.RunWorkerCompleted += worker_RunWorkerCompleted;

            foreach (var pattern in Settings.SearchPatterns)
            {
                cmbFileTypes.Items.Add(pattern.Description + " " + pattern.Pattern);
            }

            cmbFileTypes.SelectedIndex = 0;
        }        

        private string Path1
        {
            get
            {
                return tbFolderPath1.Text.Trim();
            }
        }

        void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            btnCancel.Enabled = false;
            pbFilesProcessed.Value = 100;
            CheckEnabled(this, EventArgs.Empty);            

            if (workerWasCancelled)
            {
                workerWasCancelled = false;
                lblProgress.Text = Properties.Resources.Cancelled;
                return;
            }


            if (possibleMatches.Count == 0)
            {
                lblProgress.Text = "";
            }
            else
            {
                foreach (KeyValuePair<string, List<FileHashPair>> possibleMatch in possibleMatches)
                {                    
                    compareResultsControl.AddMatches(possibleMatch.Value);
                }
            }

            compareResultsControl.AutosizeMatches();
            compareResultsControl.Focus();

            tbResultCount.Text = compareResultsControl.MatchCount.ToString("D");
            lblSpaceWasted.Text = GetTotalWastedSpaceInMb().ToString("N0") + " MB";
        }

        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pbFilesProcessed.Value = e.ProgressPercentage;
            ProgressInfo info = (ProgressInfo)e.UserState;
            lblProgress.Text = String.Format(Properties.Resources.ProcessingFile, info.FilesProcessed, info.TotalNumberOfFiles);
        }

        void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            possibleMatches.Clear();

            possibleMatches = CompareUtils.GetAllPossibleFileMatches(Path1, selectedPattern.Pattern, worker);
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            compareResultsControl.ClearMatches();
            lblProgress.Text = Properties.Resources.SearchingForFiles;
            pbFilesProcessed.Value = 1;
            btnStart.Enabled = false;
            btnCancel.Enabled = true;
            worker.RunWorkerAsync();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            btnCancel.Enabled = false;
            workerWasCancelled = true;
            worker.CancelAsync();
        }

        private void CheckEnabled(object sender, EventArgs e)
        {
            btnStart.Enabled = Path1 != "";
            btnDeleteDuplicates.Enabled = possibleMatches.Count > 0;
        }

        private void btnSelectFolder1_Click(object sender, EventArgs e)
        {
            if (Path1 != "")
            {
                fdbBrowseFolder.SelectedPath = Path1;
            }

            if (fdbBrowseFolder.ShowDialog() == DialogResult.OK)
            {
                tbFolderPath1.Text = fdbBrowseFolder.SelectedPath;
            }
        }


        private void cmbFileTypes_SelectedIndexChanged(object sender, EventArgs e)
        {
            selectedPattern = Settings.SearchPatterns[cmbFileTypes.SelectedIndex];
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void aboutFileComparerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var dlg = new AboutDialog();
            dlg.ShowDialog(this);
        }

        private void compareResultsControl_MatchClicked(object sender, Controls.EventArguments.MatchClickEventArgs args)
        {
            foreach (FileHashPair file in args.Files)
            {
                System.Diagnostics.Process.Start("explorer.exe", "/select, " + "\"" + file.FileName + "\"");
            }
        }

        private void btnDeleteDuplicates_Click(object sender, EventArgs e)
        {
            var dlgResult = MessageBox.Show("You are about to delete all duplicates found above. Only one copy in each box will be stored. PLEASE NOTE THAT THIS CAN NOT BE UNDONE.",
                                            "WARNING!",
                                            MessageBoxButtons.OKCancel,
                                            MessageBoxIcon.Warning);
            if (dlgResult == System.Windows.Forms.DialogResult.OK)
	        {
                foreach (var possibleMatchKey in possibleMatches.Keys)
                {
                    List<FileHashPair> fileList = possibleMatches[possibleMatchKey];
                    var filePaths = fileList.Select(x => x.FileName).ToList();
                    for (int i = 0; i < filePaths.Count - 1; i++)
                    {
                        var filePath = filePaths[i];
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("Error deleting file " + filePath + ". Exception: " + ex);
                        }
                    }
                }
	        }
        }

        private int GetTotalWastedSpaceInMb()
        {
            double totalWastedSpace = 0;

            foreach (var possibleMatchKey in possibleMatches.Keys)
            {
                List<FileHashPair> fileList = possibleMatches[possibleMatchKey];
                double singleFileSize = fileList[0].FileSizeMBytes;
                totalWastedSpace += singleFileSize * (fileList.Count - 1);
            }

            return (int)totalWastedSpace;
        }
    }
}