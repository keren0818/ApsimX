﻿using Models.Core;
using Models.CLEM.Resources;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models.CLEM.Groupings;
using Models.Core.Attributes;

namespace Models.CLEM.Activities
{
    /// <summary>Ruminant wean activity</summary>
    /// <summary>This activity will wean the herd</summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(CLEMActivityBase))]
    [ValidParent(ParentType = typeof(ActivitiesHolder))]
    [ValidParent(ParentType = typeof(ActivityFolder))]
    [Description("This activity manages weaning of suckling ruminant individuals.")]
    [Version(1, 0, 1, "Adam Liedloff", "CSIRO", "")]
    public class RuminantActivityWean: CLEMRuminantActivityBase
    {
        /// <summary>
        /// Weaning age (months)
        /// </summary>
        [Description("Weaning age (months)")]
        [Required, GreaterThanEqualValue(1)]
        public double WeaningAge { get; set; }

        /// <summary>
        /// Weaning weight (kg)
        /// </summary>
        [Description("Weaning weight (kg)")]
        [Required, GreaterThanEqualValue(0)]
        public double WeaningWeight { get; set; }

        /// <summary>
        /// Name of GrazeFoodStore (paddock) to place weaners (leave blank for general yards)
        /// </summary>
        [Description("Name of GrazeFoodStore (paddock) to place weaners in")]
        [Models.Core.Display(Type = DisplayTypeEnum.CLEMResourceName, CLEMResourceNameResourceGroups = new Type[] { typeof(GrazeFoodStore) }, CLEMExtraEntries = new string[] { "Not specified - general yards" })]
        public string GrazeFoodStoreName { get; set; }
        
        private string grazeStore; 
        
        /// <summary>
        /// Constructor
        /// </summary>
        public RuminantActivityWean()
        {
            this.SetDefaults();
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMInitialiseActivity")]
        private void OnCLEMInitialiseActivity(object sender, EventArgs e)
        {
            this.InitialiseHerd(false, true);

            // check GrazeFoodStoreExists
            grazeStore = "";
            if (GrazeFoodStoreName != null && !GrazeFoodStoreName.StartsWith("Not specified"))
            {
                grazeStore = GrazeFoodStoreName.Split('.').Last();
                var foodStore = Resources.GetResourceItem(this, typeof(GrazeFoodStore), GrazeFoodStoreName, OnMissingResourceActionTypes.Ignore, OnMissingResourceActionTypes.ReportErrorAndStop) as GrazeFoodStoreType;
            }
        }

        /// <summary>An event handler to call for all herd management activities</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("CLEMAnimalManage")]
        private void OnCLEMAnimalManage(object sender, EventArgs e)
        {
            // if management month
            if (this.TimingOK)
            {
                double labourlimit = this.LabourLimitProportion;
                int weanedCount = 0;
                ResourceRequest labour = ResourceRequestList.Where(a => a.ResourceType == typeof(LabourType)).FirstOrDefault<ResourceRequest>();
                // Perform weaning
                int count = this.CurrentHerd(true).Where(a => a.Weaned == false).Count();
                foreach (var ind in this.CurrentHerd(true).Where(a => a.Weaned == false))
                {
                    if (ind.Age >= WeaningAge || ind.Weight >= WeaningWeight)
                    {
                        ind.Wean();
                        ind.Location = grazeStore;
                        weanedCount++;
                        Status = ActivityStatus.Success;
                    }

                    // stop if labour limited individuals reached and LabourShortfallAffectsActivity
                    if (weanedCount > Convert.ToInt32(count * labourlimit)) break;
                }
            }
        }

        /// <summary>
        /// Determine the labour required for this activity based on LabourRequired items in tree
        /// </summary>
        /// <param name="Requirement">Labour requirement model</param>
        /// <returns></returns>
        public override double GetDaysLabourRequired(LabourRequirement Requirement)
        {
            List<Ruminant> herd = CurrentHerd(false);
            int head = this.CurrentHerd(true).Where(a => a.Weaned == false).Count();

            double daysNeeded = 0;
            switch (Requirement.UnitType)
            {
                case LabourUnitType.Fixed:
                    daysNeeded = Requirement.LabourPerUnit;
                    break;
                case LabourUnitType.perHead:
                    daysNeeded = head * Requirement.LabourPerUnit;
                    break;
                case LabourUnitType.perUnit:
                    double numberUnits = head / Requirement.UnitSize;
                    if (Requirement.WholeUnitBlocks) numberUnits = Math.Ceiling(numberUnits);
                    daysNeeded = numberUnits * Requirement.LabourPerUnit;
                    break;
                default:
                    throw new Exception(String.Format("LabourUnitType {0} is not supported for {1} in {2}", Requirement.UnitType, Requirement.Name, this.Name));
            }
            return daysNeeded;
        }

        /// <summary>
        /// Method to determine resources required for this activity in the current month
        /// </summary>
        /// <returns>List of required resource requests</returns>
        public override List<ResourceRequest> GetResourcesNeededForActivity()
        {
            return null;
        }

        /// <summary>
        /// Method used to perform activity if it can occur as soon as resources are available.
        /// </summary>
        public override void DoActivity()
        {
            Status = ActivityStatus.NotNeeded;
            return;
        }

        /// <summary>
        /// Method to determine resources required for initialisation of this activity
        /// </summary>
        /// <returns></returns>
        public override List<ResourceRequest> GetResourcesNeededForinitialisation()
        {
            return null;
        }

        /// <summary>
        /// The method allows the activity to adjust resources requested based on shortfalls (e.g. labour) before they are taken from the pools
        /// </summary>
        public override void AdjustResourcesNeededForActivity()
        {
            return;
        }

        /// <summary>
        /// Resource shortfall event handler
        /// </summary>
        public override event EventHandler ResourceShortfallOccurred;

        /// <summary>
        /// Shortfall occurred 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnShortfallOccurred(EventArgs e)
        {
            if (ResourceShortfallOccurred != null)
                ResourceShortfallOccurred(this, e);
        }

        /// <summary>
        /// Resource shortfall occured event handler
        /// </summary>
        public override event EventHandler ActivityPerformed;

        /// <summary>
        /// Shortfall occurred 
        /// </summary>
        /// <param name="e"></param>
        protected override void OnActivityPerformed(EventArgs e)
        {
            if (ActivityPerformed != null)
                ActivityPerformed(this, e);
        }

        /// <summary>
        /// Provides the description of the model settings for summary (GetFullSummary)
        /// </summary>
        /// <param name="FormatForParentControl">Use full verbose description</param>
        /// <returns></returns>
        public override string ModelSummary(bool FormatForParentControl)
        {
            string html = "";
            html += "\n<div class=\"activityentry\">Individuals are weaned at ";
            html += "<span class=\"setvalue\">" + WeaningAge.ToString("#0.#") + "</span> months or ";
            html += "<span class=\"setvalue\">" + WeaningWeight.ToString("##0.##") + "</span> kg";
            html += "</div>";
            html += "\n<div class=\"activityentry\">Weaned individuals will be placed in ";
            if (GrazeFoodStoreName == null || GrazeFoodStoreName == "")
            {
                html += "<span class=\"resourcelink\">Not specified - general yards</span>";
            }
            else
            {
                html += "<span class=\"resourcelink\">" + GrazeFoodStoreName + "</span>";
            }
            html += "</div>";
            return html;
        }
    }
}
