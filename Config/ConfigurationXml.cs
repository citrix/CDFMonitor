// ***********************************************************************
// Assembly : CDFMonitor Author : cdfmdev Created : 07-06-2013
//
// Last Modified By : cdfmdev Last Modified On : 07-06-2013
// ***********************************************************************
// <copyright file="ConfigurationXml.cs" company=""> Copyright (c) 2014 Citrix Systems, Inc.
// </copyright> <summary></summary>
// ***********************************************************************
namespace CDFM.Config
{
    using CDFM.Engine;
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Xml.Linq;

    /// <summary>
    /// Class Xml
    /// </summary>
    internal class ConfigurationXml
    {
        #region Private Fields

        private XDocument _doc;
        private string _docRoot = "configuration"; // dont change or it will break app.config
        private string[] _dotNetSupportedVers = new string[] { "v4.5", "v4.0.30319", "v2.0.50727" };
        private string _elementRoot = "appSettings"; // dont change or it will break app.config
        private string _startupRoot = "startup"; // dont change or it will break app.config
        private string _xmlFileName = Process.GetCurrentProcess().MainModule.ModuleName + ".config";
        private XElement _xmlTree;

        #endregion Private Fields

        #region Public Constructors

        // private string file;
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationXml" /> class.
        /// </summary>
        /// <param name="file">The file.</param>
        public ConfigurationXml(string file = null)
        {
            if (!string.IsNullOrEmpty(file))
            {
                _xmlFileName = file;
            }

            if (File.Exists(_xmlFileName))
            {
                _doc = XDocument.Load(_xmlFileName);
            }
            else
            {
                _doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
                            new XComment(string.Format("CdfMonitor:{0}:{1}",
                                 Assembly.GetExecutingAssembly().GetName().Version.ToString(),
                                Environment.MachineName)),
                            new XElement(_docRoot));

                // static config data
                List<XElement> xElements = new List<XElement>();

                for (int i = 0; i < _dotNetSupportedVers.Length; i++)
                {
                    xElements.Add(new XElement("supportedRuntime", new XAttribute("version", _dotNetSupportedVers[i])));
                }

                _doc.Root.Add(_xmlTree, new XElement(_startupRoot, xElements.ToArray()));
            }
        }

        #endregion Public Constructors

        #region Public Methods

        /// <summary>
        /// Adds the XML node.
        /// </summary>
        /// <param name="prop">The prop.</param>
        /// <param name="val">The val.</param>
        public void AddXmlNode(string prop, string val)
        {
            try
            {
                RemoveXmlNode(prop);
                _xmlTree = new XElement(_elementRoot, new XElement("add", new XAttribute[] { new XAttribute("key", prop), new XAttribute("value", val) }));
                _doc.Root.Add(_xmlTree);
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("AddXmlNode:exception" + e.ToString());
            }
        }

        /// <summary>
        /// Adds the XML nodes.
        /// </summary>
        /// <param name="col">The col.</param>
        public void AddXmlNodes(KeyValueConfigurationCollection col)
        {
            try
            {
                List<XAttribute> xAttributes = new List<XAttribute>();
                List<XElement> xElements = new List<XElement>();
                foreach (KeyValueConfigurationElement element in col)
                {
                    xElements.Add(new XElement("add", new XAttribute[] { new XAttribute("key", element.Key), new XAttribute("value", element.Value) }));
                }

                _doc.Root.Add(_xmlTree, new XElement(_elementRoot, xElements.ToArray()));
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("AddXmlNodes:exception" + e.ToString());
            }
        }

        /// <summary>
        /// Removes the XML node.
        /// </summary>
        /// <param name="prop">The prop.</param>
        public void RemoveXmlNode(string prop)
        {
            if (_doc.Root.Elements().Count() < 1)
            {
                return;
            }

            IEnumerable<XElement> res = _doc.Descendants(prop)
                .Where(element => element.Value == prop);

            foreach (XElement x in new List<XElement>(res))
            {
                x.Parent.Parent.RemoveAll();
            }
        }

        /// <summary>
        /// Saves this instance.
        /// </summary>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise</returns>
        public bool Save()
        {
            try
            {
                _doc.Save(_xmlFileName);
                return true;
            }
            catch (Exception e)
            {
                CDFMonitor.LogOutputHandler("Save:exception" + e.ToString());
                return false;
            }
        }

        #endregion Public Methods
    }
}