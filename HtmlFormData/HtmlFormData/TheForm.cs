﻿using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HtmlFormData.Properties;
using Microsoft.Office.Core;
using Visio = Microsoft.Office.Interop.Visio;

namespace HtmlFormData
{
    public partial class TheForm : Form
    {
        private int _currentShapeId;
        private bool _needUpdate;
        private readonly Visio.Window _window;

        static string GetResourcePath(string fileName)
        {
            var assemblyPath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            return Path.Combine(assemblyDirectory ?? string.Empty, "Resources", fileName);
        }

        /// <summary>
        /// Form constructor, receives parent Visio diagram window
        /// </summary>
        /// <param name="window">Parent Visio diagram window</param>
        public TheForm(Visio.Window window)
        {
            _window = window;
            InitializeComponent();

            NativeMethods.DisableClickSounds(true);
            // webBrowser.ObjectForScripting = this;
            
            webBrowser.Navigate(new Uri(GetResourcePath(@"edit.html")));
            webBrowser.DocumentCompleted += WebBrowserOnDocumentCompleted;
            _window.SelectionChanged += WindowOnSelectionChanged;
            _window.Application.VisioIsIdle += ApplicationVisioIsIdle;

            Closed += OnClosed;
        }

        private void ApplicationVisioIsIdle(Visio.Application app)
        {
            if (_needUpdate)
            {
                try
                {
                    UpdateSidebar();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
                _needUpdate = false;
            }
        }

        private void OnClosed(object sender, EventArgs eventArgs)
        {
            _window.SelectionChanged -= WindowOnSelectionChanged;
            _window.Application.VisioIsIdle -= ApplicationVisioIsIdle;
            NativeMethods.DisableClickSounds(false);
        }

        private void WebBrowserOnDocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs webBrowserDocumentCompletedEventArgs)
        {
            var document = webBrowser.Document;

            if (document == null)
                return;

            var newCurrentShape = _window.Selection.PrimaryItem;
            _currentShapeId = newCurrentShape != null ? newCurrentShape.ID : 0;

            var targetCell = newCurrentShape != null ? GetTargetCell(newCurrentShape) : null;
            if (targetCell != null)
            {
                var html = GetCellText(targetCell);
                document.InvokeScript("setEditorHtml", new object [] { html });
            }
            else
            {
                document.InvokeScript("disableEditor");
            }
        }

        private void WindowOnSelectionChanged(Visio.Window window)
        {
            _needUpdate = true;
        }

        void UpdateSidebar()
        {
            var document = webBrowser.Document;

            if (document == null)
                return;

            if (_currentShapeId != 0)
            {
                var currentShape = _window.PageAsObj.Shapes.ItemFromID[_currentShapeId];
                if (currentShape != null)
                {
                    var targetCell = GetTargetCell(currentShape);
                    if (targetCell != null)
                    {
                        var newText = document.InvokeScript("getEditorHtml").ToString();
                        var oldText = GetCellText(targetCell);
                        if (newText != oldText)
                            SetCellText(targetCell, newText);
                    }

                    var targetPlainTextCell = GetTargetPlainTextCell(currentShape);
                    if (targetPlainTextCell != null)
                    {
                        var newText = document.InvokeScript("getEditorText").ToString().Replace("\r\n\r\n", "\r\n");
                        var oldText = GetCellText(targetPlainTextCell);
                        if (newText != oldText)
                            SetCellText(targetPlainTextCell, newText);
                    }
                }
            }

            webBrowser.Navigate(new Uri(GetResourcePath(@"edit.html")));
        }

        private Visio.Cell GetTargetCell(Visio.Shape shape)
        {
            var propName = Settings.Default.PropertyName;
            return !string.IsNullOrEmpty(propName) && shape.CellExistsU[propName, 0] != 0 ? shape.CellsU[propName] : null;
        }
        private Visio.Cell GetTargetPlainTextCell(Visio.Shape shape)
        {
            var propNamePlainText = Settings.Default.PropertyNamePlainText;
            return !string.IsNullOrEmpty(propNamePlainText) && shape.CellExistsU[propNamePlainText, 0] != 0 ? shape.CellsU[propNamePlainText] : null;
        }

        private void SetCellText(Visio.Cell cell, string text)
        {
            if (cell != null)
                cell.FormulaU = text != null ? "\"" + text.Replace("\"", "\"\"") + "\"" : "No Formula";
        }

        private string GetCellText(Visio.Cell cell)
        {
            return cell.ResultStrU[0];
        }
    }
}
