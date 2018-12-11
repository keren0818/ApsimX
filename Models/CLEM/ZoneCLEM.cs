﻿using Models.CLEM.Activities;
using Models.CLEM.Resources;
using Models.Core;
using Models.Core.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Models.CLEM
{
    /// <summary>
    /// CLEM Zone to control simulation
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(Simulation))]
    [ValidParent(ParentType = typeof(Zone))]
    [Description("This manages all CLEM resources and activities in the simulation.")]
    [HelpUri("http://www.csiro.au")]
    [Version(1,0,1,"","CSIRO","")]
    public class ZoneCLEM: Zone, IValidatableObject
    {
        [Link]
        ISummary Summary = null;
        [Link]
        Clock Clock = null;
        [Link]
        Simulation Simulation = null;

        /// <summary>
        /// Seed for random number generator (0 uses clock)
        /// </summary>
        [System.ComponentModel.DefaultValueAttribute(1)]
        [Required, GreaterThanEqualValue(0) ]
        [Description("Random number generator seed (0 to use clock)")]
        public int RandomSeed { get; set; }

        private static Random randomGenerator;

        /// <summary>
        /// Access the CLEM random number generator
        /// </summary>
        [XmlIgnore]
        [Description("Random number generator for simulation")]
        public static Random RandomGenerator { get { return randomGenerator; } }

        /// <summary>
        /// Index of the simulation Climate Region
        /// </summary>
        [Description("Climate region index")]
        public int ClimateRegion { get; set; }

        /// <summary>
        /// Ecological indicators calculation interval (in months, 1 monthly, 12 annual)
        /// </summary>
        [System.ComponentModel.DefaultValueAttribute(12)]
        [Description("Ecological indicators calculation interval (in months, 1 monthly, 12 annual)")]
        [XmlIgnore]
        public int EcologicalIndicatorsCalculationInterval { get; set; }

        /// <summary>
        /// End of month to calculate ecological indicators
        /// </summary>
        [System.ComponentModel.DefaultValueAttribute(7)]
        [Description("End of month to calculate ecological indicators")]
        [Required, Month]
        public int EcologicalIndicatorsCalculationMonth { get; set; }

        /// <summary>
        /// Month this overhead is next due.
        /// </summary>
        [XmlIgnore]
        public DateTime EcologicalIndicatorsNextDueDate { get; set; }

        // ignore zone base class properties

        /// <summary>Area of the zone.</summary>
        /// <value>The area.</value>
        [XmlIgnore]
        public new double Area { get; set; }
        /// <summary>Gets or sets the slope.</summary>
        /// <value>The slope.</value>
        [XmlIgnore]
        public new double Slope { get; set; }

        /// <summary>
        /// Validate object
        /// </summary>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();
            if (Clock.StartDate.Day != 1)
            {
                string[] memberNames = new string[] { "Clock.StartDate" };
                results.Add(new ValidationResult(String.Format("CLEM must commence on the first day of a month. Invalid start date {0}", Clock.StartDate.ToShortDateString()), memberNames));
            }
            // check that one resources and on activities are present.
            int holderCount = this.Children.Where(a => a.GetType() == typeof(ResourcesHolder)).Count();
            if (holderCount == 0)
            {
                string[] memberNames = new string[] { "CLEM.Resources" };
                results.Add(new ValidationResult("CLEM must contain a Resources Holder to manage resources", memberNames));
            }
            if (holderCount > 1)
            {
                string[] memberNames = new string[] { "CLEM.Resources" };
                results.Add(new ValidationResult("CLEM must contain only one (1) Resources Holder to manage resources", memberNames));
            }
            holderCount = this.Children.Where(a => a.GetType() == typeof(ActivitiesHolder)).Count();
            if (holderCount == 0)
            {
                string[] memberNames = new string[] { "CLEM.Activities" };
                results.Add(new ValidationResult("CLEM must contain an Activities Holder to manage activities", memberNames));
            }
            if (holderCount > 1)
            {
                string[] memberNames = new string[] { "CLEM.Activities" };
                results.Add(new ValidationResult("CLEM must contain only one (1) Activities Holder to manage activities", memberNames));
            }
            return results;
        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("StartOfSimulation")]
        private void OnStartOfSimulation(object sender, EventArgs e)
        {
            // validation is performed here
            // commencing is too early as Summary has not been created for reporting.
            // some values assigned in commencing will not be checked before processing, but will be caught here
            if (!Validate(Simulation, ""))
            {
                string error = "@i:Invalid parameters in model";
                throw new ApsimXException(this, error);
            }

            if (EcologicalIndicatorsCalculationMonth >= Clock.StartDate.Month)
            {
                // go back from start month in intervals until
                DateTime trackDate = new DateTime(Clock.StartDate.Year, EcologicalIndicatorsCalculationMonth, Clock.StartDate.Day);
                while (trackDate.AddMonths(-EcologicalIndicatorsCalculationInterval) >= Clock.Today)
                {
                    trackDate = trackDate.AddMonths(-EcologicalIndicatorsCalculationInterval);
                }
                EcologicalIndicatorsNextDueDate = trackDate;
            }
            else
            {
                EcologicalIndicatorsNextDueDate = new DateTime(Clock.StartDate.Year, EcologicalIndicatorsCalculationMonth, Clock.StartDate.Day);
                while (Clock.StartDate > EcologicalIndicatorsNextDueDate)
                {
                    EcologicalIndicatorsNextDueDate = EcologicalIndicatorsNextDueDate.AddMonths(EcologicalIndicatorsCalculationInterval);
                }
            }

        }

        /// <summary>An event handler to allow us to initialise ourselves.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            if (RandomSeed==0)
            {
                randomGenerator = new Random();
            }
            else
            {
                randomGenerator = new Random(RandomSeed);
            }
            EcologicalIndicatorsCalculationInterval = 12;
        }

        /// <summary>
        /// Internal method to iterate through all children in CLEM and report any parameter setting errors
        /// </summary>
        /// <param name="model"></param>
        /// <param name="modelPath">Pass blank string. Used for tracking model path</param>
        /// <returns>Boolean indicating whether validation was successful</returns>
        private bool Validate(Model model, string modelPath)
        {
            string starter = "[";
            if(typeof(IResourceType).IsAssignableFrom(model.GetType()))
            {
                starter = "[r=";
            }
            if(model.GetType() == typeof(ResourcesHolder))
            {
                starter = "[r=";
            }
            if (model.GetType().IsSubclassOf(typeof(ResourceBaseWithTransactions)))
            {
                starter = "[r=";
            }
            if (model.GetType() == typeof(ActivitiesHolder))
            {
                starter = "[a=";
            }
            if (model.GetType().IsSubclassOf(typeof(CLEMActivityBase)))
            {
                starter = "[a=";
            }

            modelPath += starter+model.Name+"]";
            modelPath = modelPath.Replace("][", "]&shy;[");
            bool valid = true;
            var validationContext = new ValidationContext(model, null, null);
            var validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(model, validationContext, validationResults, true);
            if (validationResults.Count > 0)
            {
                valid = false;
                // report all errors
                foreach (var validateError in validationResults)
                {
                    // get description
                    string text = "";
                    var property = model.GetType().GetProperty(validateError.MemberNames.FirstOrDefault());
                    if (property != null)
                    {
                        var attribute = property.GetCustomAttributes(typeof(DescriptionAttribute), true)[0];
                        var description = (DescriptionAttribute)attribute;
                        text = description.ToString();
                    }
                    string error = String.Format("@validation:Invalid parameter value in " + modelPath + "" + Environment.NewLine + "PARAMETER: " + validateError.MemberNames.FirstOrDefault());
                    if (text != "")
                    {
                        error += String.Format(Environment.NewLine + "DESCRIPTION: " + text );
                    }
                    error += String.Format(Environment.NewLine + "PROBLEM: " + validateError.ErrorMessage + Environment.NewLine);
                    Summary.WriteWarning(this, error);
                }
            }
            foreach (var child in model.Children)
            {
                bool result = Validate(child, modelPath);
                if (valid & !result)
                {
                    valid = false;
                }
            }
            return valid;
        }

            /// <summary>
        /// 
        /// </summary>
        /// <param name="model"></param>
        /// <param name="useFullDescription">Use full verbose description</param>
        /// <param name="htmlString"></param>
        /// <returns></returns>
        public string GetFullSummary(object model, bool useFullDescription, string htmlString)
        {
            string html = "";
            html += "\n<div class=\"holdermain\">";
            html += "\n<div class=\"clearfix defaultbanner\">";
            html += "<div class=\"typediv\">" + this.GetType().Name + "</div>";
            html += "</div>";
            html += "\n<div class=\"defaultcontent\">";
            html += "\n<div class=\"activityentry\">Random numbers are used in this simultion. ";
            if (RandomSeed == 0)
            {
                html += "Every run of this simulation will be different.";
            }
            else
            {
                html += "Each run of this simulation will be identical using the seed <span class=\"setvalue\">" + RandomSeed.ToString() + "</span>";
            }
            html += "\n</div>";
            html += "\n</div>";

            // get clock
            IModel parentSim = Apsim.Parent(this, typeof(Simulation));
            Clock clk = Apsim.Children(parentSim, typeof(Clock)).FirstOrDefault() as Clock;

            html += "\n<div class=\"clearfix defaultbanner\">";
            html += "<div class=\"namediv\">" + clk.Name + "</div>";
            html += "<div class=\"typediv\">Clock</div>";
            html += "</div>";
            html += "\n<div class=\"defaultcontent\">";
            html += "\n<div class=\"activityentry\">This simulation runs from ";
            if (clk.StartDate == null)
            {
                html += "<span class=\"errorlink\">[START DATE NOT SET]</span>";
            }
            else
            {
                html += "<span class=\"setvalue\">" + clk.StartDate.ToShortDateString() + "</span>";
            }
            html += " to ";
            if (clk.EndDate == null)
            {
                html += "<span class=\"errorlink\">[END DATE NOT SET]</span>";
            }
            else
            {
                html += "<span class=\"setvalue\">" + clk.EndDate.ToShortDateString() + "</span>";
            }
            html += "\n</div>";
            html += "\n</div>";
            html += "\n</div>";

            foreach (CLEMModel cm in Apsim.Children(this, typeof(CLEMModel)).Cast<CLEMModel>())
            {
                html += cm.GetFullSummary(cm, true, "");
            }
            return html;
        }

        /// <summary>
        /// Method to determine if this is the month to calculate ecological indicators
        /// </summary>
        /// <returns></returns>
        public bool IsEcologicalIndicatorsCalculationMonth()
        {
            return this.EcologicalIndicatorsNextDueDate.Year == Clock.Today.Year & this.EcologicalIndicatorsNextDueDate.Month == Clock.Today.Month;
        }

        /// <summary>Data stores to clear at start of month</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("EndOfMonth")]
        private void OnEndOfMonth(object sender, EventArgs e)
        {
            if(IsEcologicalIndicatorsCalculationMonth())
            {
                this.EcologicalIndicatorsNextDueDate = this.EcologicalIndicatorsNextDueDate.AddMonths(this.EcologicalIndicatorsCalculationInterval);
            }
        }

    }
}
