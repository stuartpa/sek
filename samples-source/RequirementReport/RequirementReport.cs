/// This is a user customized post processing sample, which extracts the requirement
/// statistics from a set of transition systems.
/// This post processing sample will generate a requirement report under c:\temp

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Xsl;
using System.IO;

using Microsoft.SpecExplorer.ObjectModel;


namespace PostProcessorSample
{
    /// <summary>
    /// Action class, which contains the action name and all covered requirements
    /// </summary>
    public class Action
    {
        /// <summary>
        /// action name
        /// </summary>
        [XmlElement("Action")]
        public string actionName;

        /// <summary>
        /// covered requirement set
        /// </summary>
        [XmlElement("Requirement")]
        public HashSet<string> requirementSet;

        public Action()
        {
            requirementSet = new HashSet<string>();
        }

        public Action(string name, HashSet<string> requirements)
        {
            actionName = name;
            requirementSet = requirements;
        }
    }

    /// <summary>
    /// The requirement table which contains all requirements covered by a collection of transition systems
    /// </summary>
    [XmlRoot("RequirementTable")]
    public class RequirementTable
    {
        /// <summary>
        /// requirement id
        /// </summary>
        [XmlElement("Requirement")]
        public HashSet<string> requirementId;

        /// <summary>
        /// action set
        /// </summary>
        [XmlElement("ActionCoveredRequirement")]
        public HashSet<Action> actionTable;

        public RequirementTable()
        {
            requirementId = new HashSet<string>();
            actionTable = new HashSet<Action>();
        }

        /// <summary>
        /// Add a set of actions into actionTable
        /// </summary>
        /// <param name="actions">The dictionary containing actions</param>
        public void AddActions(Dictionary<string, HashSet<string>> actions)
        {
            foreach (KeyValuePair<string, HashSet<string>> action in actions)
            {
                actionTable.Add(new Action(action.Key, action.Value));
            }
        }

    }

    /// <summary>
    /// The post processor which implements IPostProcessor
    /// </summary>
    public class RequirementGenerator : IPostProcessor
    {
        /// <summary>
        /// The description of the post-processing, which will be displayed in Exploration Manager
        /// </summary>
        public string Description
        {
            get { return "Generate Requirement Report"; }
        }

        /// <summary>
        /// Post-processing method
        /// </summary>
        /// <param name="transitionSystems">transition systems provided for post processing</param>
        public void PostProcess(IEnumerable<TransitionSystem> transitionSystems, IDictionary<string, object> environment)
        {
            if (transitionSystems == null)
                throw new PostProcessorException(@"transition systems is null");
            if (environment == null)
                throw new PostProcessorException(@"environment property bag is null");

            if (!environment.ContainsKey("WorkingDirectory"))
                throw new PostProcessorException(@"environment property bag does not contain WorkingDirectory");

            // Initialize requirement table
            RequirementTable requirementReport = new RequirementTable();

            /// The path of xsl file
            string xslPath = @"c:\temp\requirement.xsl";
            if (!File.Exists(xslPath))
                throw new PostProcessorException(@"xsl file not found under c:\temp");

            /// The path of requirement report
            /// environment["WorkingDirectory"] is the built-in property indicating the corresponding model project folder
            string xmlReportPath = environment["WorkingDirectory"] + "\\requirement.xml";
            string htmlReportPath = environment["WorkingDirectory"] + "\\requirement.html";
            

            HashSet<string> requirementSet = new HashSet<string>();
            Dictionary<string, HashSet<string>> actions = new Dictionary<string, HashSet<string>>();

            // Traverse all transitions
            foreach (TransitionSystem ts in transitionSystems)
            {
                // Get the covered requirement details
                foreach (Transition transition in ts.Transitions)
                {
                    string typeName = transition.Action.Symbol.Member.DeclaringType.FullName;
                    string actionName = transition.Action.Symbol.Member.Name;
                    string actionFullName = typeName + "." + actionName;
                    foreach (string requirement in transition.CapturedRequirements)
                    {
                        if (!actions.ContainsKey(actionFullName))
                            actions.Add(actionFullName, new HashSet<string>());
                        // Requirements covered by specific actions
                        actions[actionFullName].Add(requirement);
                        // Requirements covered by transition systems
                        requirementSet.Add(requirement);
                    }
                }
            }

            // Construct requirement table
            requirementReport.requirementId = requirementSet;
            requirementReport.AddActions(actions);

            // Initialize xml serializer
            XmlSerializer reqSerializer = new XmlSerializer(typeof(RequirementTable));
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.CheckCharacters = true;
            settings.Encoding = Encoding.Unicode;
            settings.Indent = true;

            // Generate xml report
            XmlWriter xmlWriter = XmlWriter.Create(xmlReportPath, settings);
            reqSerializer.Serialize(xmlWriter, requirementReport);
            xmlWriter.Close();

            // Load XSL
            XslCompiledTransform reqTransform = new XslCompiledTransform();
            reqTransform.Load(xslPath);

            // Generate html report
            reqTransform.Transform(xmlReportPath, htmlReportPath);

        }

        public void Dispose()
        {
            /// Do nothing
        }

    }
}
