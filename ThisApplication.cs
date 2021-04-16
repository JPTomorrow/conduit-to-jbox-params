using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using JPMorrow.Tools.Revit.MEP.Selection;
using JPMorrow.Revit.ConduitRuns;
using JPMorrow.Tools.Diagnostics;
using JPMorrow.Revit.Documents;
using System.Windows.Forms;

namespace MainApp
{
	public struct IndexedSelection
	{
		public int idx;
		public Reference pick_reference;
	}

	[Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.DB.Macros.AddInId("58F7B2B7-BF6D-4B39-BBF8-13F7D9AAE97E")]
	public partial class ThisApplication : IExternalCommand
	{
		public Result Execute(ExternalCommandData cData, ref string message, ElementSet elements)
        {
			string[] dataDirectories = new string[0];
			bool debugApp = false;

			// set revit documents
			ModelInfo revit_info = ModelInfo.StoreDocuments(cData, dataDirectories, debugApp);

			// get user to select conduit
            Reference selected_conduit = revit_info.UIDOC.Selection.PickObject(ObjectType.Element, new ConduitSelectionFilter(revit_info.DOC),  "Select a Conduit");

			if(selected_conduit == null) return Result.Succeeded;

			// separate first conduit from the rest
			Element conToProp = revit_info.DOC.GetElement(selected_conduit);

			// local functions
			Parameter p(Element x, string str) => x.LookupParameter(str);
			bool p_null(Element x, string str) => p(x, str) == null;

			// check for parameters and quit if null
			if (p_null(conToProp, "From") || p_null(conToProp, "To") ||
				p_null(conToProp, "Wire Size") || p_null(conToProp, "Comments") || p_null(conToProp, "Set(s)"))
			{
				debugger.show(
					header: "Conduit To Jbox Params", sub: "Parameters",
					err: "You do not have the 'To', 'From', 'Wire Size', or 'Set(s)' parameters loaded for conduits.");
				return Result.Succeeded;
			}

            List<ElementId> highlighted_elements = new List<ElementId>();

            using (TransactionGroup tgx = new TransactionGroup(revit_info.DOC, "Propogating parameters"))
			{
				tgx.Start();

				using (Transaction tx = new Transaction(revit_info.DOC, "clear run id"))
				{
					tx.Start();
					RunNetwork rn = new RunNetwork(revit_info, conToProp);

                    // add all runs for highlighting in model
                    highlighted_elements.AddRange(rn.RunIds.Concat(rn.FittingIds).Select(x => new ElementId(x)));

                    foreach(var id in rn.RunIds.Concat(rn.FittingIds)) 
					{
						Element el = revit_info.DOC.GetElement(new ElementId(id));
						p(el, "From").Set(		p(conToProp, "From").AsString());
						p(el, "To").Set(		p(conToProp, "To").AsString());
						p(el, "Wire Size").Set(	p(conToProp, "Wire Size").AsString());
						p(el, "Comments").Set(	p(conToProp, "Comments").AsString());
						p(el, "Set(s)").Set(	p(conToProp, "Set(s)").AsString());
					}

                    bool do_jboxes = false;
					if(rn.ConnectedJboxIds.Count > 1) {
                        var result = debugger.show_yesno(
							header: "Conduit To Jbox Params", 
							err: "There is more than one junction box attached to this conduit run. " + 
							"Would you like to push the parameters from the conduit run to both of the junction boxes?");

						if(result == DialogResult.Yes) do_jboxes = true;
                    }
					else {
                        do_jboxes = true;
                    }

					var test_box = revit_info.DOC.GetElement(new ElementId(rn.ConnectedJboxIds.First()));
					if (p_null(test_box, "From") || p_null(test_box, "To") ||
						p_null(test_box, "Wire Size") || p_null(test_box, "Comments"))
					{ 
						debugger.show(
							header: "Conduit To Jbox Params", sub: "Parameters",
							err: "You do not have the 'To', 'From', or 'Wire Size' parameters loaded for electrical fixtures.");
                        do_jboxes = false;
                    }

					if(do_jboxes) {
                        foreach (var jbox in rn.ConnectedJboxIds) { 
							Element el = revit_info.DOC.GetElement(new ElementId(jbox));
							p(el, "From").Set(		p(conToProp, "From").AsString());
							p(el, "To").Set(		p(conToProp, "To").AsString());
							p(el, "Wire Size").Set(	p(conToProp, "Wire Size").AsString());
							p(el, "Comments").Set(	p(conToProp, "Comments").AsString());
                            highlighted_elements.Add(new ElementId(jbox));
                        }
					}

                    tx.Commit();
				}
				tgx.Assimilate();
			}

			revit_info.UIDOC.Selection.SetElementIds(highlighted_elements.ToList());
			return Result.Succeeded;
        }

		#region startup
		private void Module_Startup(object sender, EventArgs e)
		{

		}

		private void Module_Shutdown(object sender, EventArgs e)
		{

		}
		#endregion

		#region Revit Macros generated code
		private void InternalStartup()
		{
			this.Startup += new System.EventHandler(Module_Startup);
			this.Shutdown += new System.EventHandler(Module_Shutdown);
		}
		#endregion
	}
}