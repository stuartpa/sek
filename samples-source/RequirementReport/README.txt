This is a sample to show the usages of user customized post-processing.

Users can implement Microsoft.SpecExplorer.ObjectModel.IPostProcessor to process transition systems. This sample shows how to extract requirement information from the transition system.

The following steps illustrate how to run requirement report processing:
1. Copy Requirement.xsl under c:\temp
2. Build the sample, copy RequirementReport.dll under $SpecExplorerInstallationPath\Extensions
3. Open a model project
4. Open Exploration Manager, select one machine and right click, check "Perform User Task" -> "Generate Requirement Report" in the context menu
5. Select "Perform User Task" -> "Perform checked tasks"
6. Requirement report of XML format and HTML format will be generated under the folders of specific model projects



