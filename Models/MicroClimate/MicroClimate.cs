﻿namespace Models
{
    using System;
    using System.Collections.Generic;
    using APSIM.Shared.Utilities;
    using Models.Core;
    using Models.Interfaces;
    using System.Linq;

    /// <summary>
    /// # [Name]
    /// The module MICROMET, described here, has been developed to allow the calculation of 
    /// potential transpiration for multiple competing canopies that can be either layered or intermingled.
    /// </summary>
    [Serializable]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    [ValidParent(ParentType = typeof(Simulation))]
    [ValidParent(ParentType = typeof(Zone))]
    public class MicroClimate : Model
    {
        /// <summary>The clock</summary>
        [Link]
        private Clock clock = null;

        /// <summary>The weather</summary>
        [Link]
        private IWeather weather = null;

        /// <summary>The sun set angle (degrees)</summary>
        private const double sunSetAngle = 0.0;

        /// <summary>The sun angle for net positive radiation (degrees)</summary>
        private const double sunAngleNetPositiveRadiation = 15;

        /// <summary>List of uptakes</summary>
        private List<MicroClimateZone> microClimatesZones;

        /// <summary>This is the length of time within the day during which evaporation will take place</summary>
        private double dayLengthEvap;

        /// <summary>This is the length of time within the day during which the sun is above the horizon</summary>
        private double dayLengthLight;

        /// <summary>Gets or sets the a_interception.</summary>
        [Description("Multiplier on rainfall to calculate interception losses")]
        [Bounds(Lower = 0.0, Upper = 10.0)]
        [Units("mm/mm")]
        public double a_interception { get; set; }

        /// <summary>Gets or sets the b_interception.</summary>
        [Description("Power on rainfall to calculate interception losses")]
        [Bounds(Lower = 0.0, Upper = 5.0)]
        [Units("-")]
        public double b_interception { get; set; }

        /// <summary>Gets or sets the c_interception.</summary>
        [Description("Multiplier on LAI to calculate interception losses")]
        [Bounds(Lower = 0.0, Upper = 10.0)]
        [Units("mm")]
        public double c_interception { get; set; }

        /// <summary>Gets or sets the d_interception.</summary>
        [Description("Constant value to add to calculate interception losses")]
        [Bounds(Lower = 0.0, Upper = 20.0)]
        [Units("mm")]
        public double d_interception { get; set; }

        /// <summary>Gets or sets the soil_albedo.</summary>
        [Description("Soil albedo")]
        [Bounds(Lower = 0.0, Upper = 1.0)]
        [Units("MJ/MJ")]
        public double soil_albedo { get; set; }

        /// <summary>Fraction of solar radiation reaching the soil surface that results in soil heating</summary>
        [Description("Fraction of solar radiation reaching the soil surface that results in soil heating")]
        [Bounds(Lower = 0.0, Upper = 1.0)]
        [Units("MJ/MJ")]
        public double SoilHeatFluxFraction { get; set; }

        /// <summary>The minimum height difference between canopies for a new layer to be created (m).</summary>
        [Description("The minimum height difference between canopies for a new layer to be created (m)")]
        [Units("m")]
        public double MinimumHeightDiffForNewLayer { get; set; } = 0.0;

        /// <summary>Height of the tallest canopy.</summary>
        [Units("mm")]
        public double CanopyHeight
        {
            get
            {
                if (microClimatesZones.Sum(n => n.Canopies.Count) == 0)
                    return 0;
                else
                    return microClimatesZones.Max(m => m.Canopies.Max(c => c.Canopy.Height));
            }
        }

        /// <summary>The fraction of intercepted rainfall that evaporates at night</summary>
        [Description("The fraction of intercepted rainfall that evaporates at night")]
        [Bounds(Lower = 0.0, Upper = 1.0)]
        [Units("0-1")]
        public double NightInterceptionFraction { get; set; }

        /// <summary>Height of the weather instruments</summary>
        [Description("Height of the weather instruments")]
        [Bounds(Lower = 0.0, Upper = 10.0)]
        [Units("m")]
        public double ReferenceHeight { get; set; }

        /// <summary>Shortwave radiation reaching the surface (ie above the residue layer) (MJ/m2)</summary>
        [Description("Shortwave radiation reaching the surface (ie above the residue layer) (MJ/m2)")]
        [Bounds(Lower = 0.0, Upper = 40.0)]
        [Units("MJ/m2")]
        public double[] SurfaceRS
        {
            get
            {
                double[] values = new double[microClimatesZones.Count];
                for (int i = 0; i < microClimatesZones.Count; i++)
                    values[i] = microClimatesZones[i].SurfaceRs;

                return values;
            }
        }

        /// <summary>Gets the amount of precipitation intercepted by the canopy (mm).</summary>
        [Description("Total intercepted precipitation")]
        [Units("mm")]
        public double PrecipitationInterception
        {
            get { return microClimatesZones[0].PrecipitationInterception; }
        }

        /// <summary>Gets the amount of radiation intercepted by the canopy (MJ/m2).</summary>
        [Description("Total intercepted radiation")]
        [Units("MJ/m^2")]
        public double RadiationInterception
        {
            get { return microClimatesZones[0].RadiationInterception; }
        }

        /// <summary>Gets the total Penman-Monteith potential evapotranspiration (MJ/m2).</summary>
        [Description("Total Penman-Monteith potential evapotranspiration")]
        [Units("MJ/m^2")]
        public double PetTotal
        {
            get { return PetRadiationTerm + PetAerodynamicTerm; }
        }

        /// <summary>Gets the radiation term of for the Penman-Monteith PET (mm).</summary>
        [Description("Radiation term of for the Penman-Monteith PET")]
        [Units("mm")]
        public double PetRadiationTerm
        {
            get { return microClimatesZones[0].petr; }
        }

        /// <summary>Gets the aerodynamic term of for the Penman-Monteith PET (mm).</summary>
        [Description("Aerodynamic term of for the Penman-Monteith PET")]
        [Units("mm")]
        public double PetAerodynamicTerm
        {
            get { return microClimatesZones[0].peta; }
        }

        /// <summary>Gets the fraction of the daytime in which the leaves are dry (0-1).</summary>
        [Description("Fraction of the daytime in which the leaves are dry")]
        [Units("-")]
        public double DryLeafTimeFraction
        {
            get { return microClimatesZones[0].DryLeafFraction; }
        }

        /// <summary>Gets the total net radiation, long and short waves (MJ/m2).</summary>
        [Description("Net radiation, long and short waves")]
        [Units("MJ/m^2")]
        public double NetRadiation
        {
            get { return NetShortWaveRadiation + NetLongWaveRadiation; }
        }


        /// <summary>Gets the net short wave radiation (MJ/m2).</summary>
        [Description("Net short wave radiation")]
        [Units("MJ/m^2")]
        public double NetShortWaveRadiation
        {
            get { return weather.Radn * (1.0 - microClimatesZones[0].Albedo); }
        }

        /// <summary>Gets the net long wave radiation (MJ/m2).</summary>
        [Description("Net long wave radiation")]
        [Units("MJ/m^2")]
        public double NetLongWaveRadiation
        {
            get { return microClimatesZones[0].NetLongWaveRadiation; }
        }

        /// <summary>Gets the flux of heat into the soil (MJ/m2).</summary>
        [Description("Soil heat flux")]
        [Units("MJ/m^2")]
        public double SoilHeatFlux
        {
            get { return microClimatesZones[0].SoilHeatFlux; }
        }

        /// <summary>Gets the total plant cover (0-1).</summary>
        [Description("Total canopy cover")]
        [Units("-")]
        public double CanopyCover
        {
            get { return microClimatesZones[0].CanopyCover; }
        }

        /// <summary>The number of canopy layers.</summary>
        [Description("Number of canopy layers")]
        public int NumLayers
        {
            get { return microClimatesZones[0].DeltaZ.Length; }
        }

        /// <summary>Called when simulation starts.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event data.</param>
        [EventSubscribe("StartOfSimulation")]
        private void OnStartOfSimulation(object sender, EventArgs e)
        {
            microClimatesZones = new List<MicroClimateZone>();
            foreach (Zone newZone in Apsim.ChildrenRecursively(this.Parent, typeof(Zone)))
                microClimatesZones.Add(new MicroClimateZone(clock, newZone, MinimumHeightDiffForNewLayer));
            if (microClimatesZones.Count == 0)
                microClimatesZones.Add(new MicroClimateZone(clock, this.Parent as Zone, MinimumHeightDiffForNewLayer));
        }

        /// <summary>Called when the canopy energy balance needs to be calculated.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoEnergyArbitration")]
        private void DoEnergyArbitration(object sender, EventArgs e)
        {
            microClimatesZones.ForEach(zone => zone.DailyInitialise(weather));

            dayLengthLight = MathUtilities.DayLength(clock.Today.DayOfYear, sunSetAngle, weather.Latitude);
            dayLengthEvap = MathUtilities.DayLength(clock.Today.DayOfYear, sunAngleNetPositiveRadiation, weather.Latitude);
            // VOS - a temporary kludge to get this running for high latitudes. MicroMet is due for a clean up soon so reconsider then.
            dayLengthEvap = Math.Max(dayLengthEvap, (dayLengthLight * 2.0 / 3.0));
            if (microClimatesZones.Count == 2 && microClimatesZones[0].Zone is Zones.RectangularZone 
                && microClimatesZones[1].Zone is Zones.RectangularZone && microClimatesZones[0].Canopies.Count > 0 && microClimatesZones[1].Canopies.Count > 0)
            {
                // We are in a strip crop simulation
                microClimatesZones[0].DoCanopyCompartments();
                microClimatesZones[1].DoCanopyCompartments();
                CalculateStripZoneShortWaveRadiation();
            }
            else // Normal 1D zones are to be used
                foreach (MicroClimateZone ZoneMC in microClimatesZones)
                {
                    ZoneMC.DoCanopyCompartments();
                    CalculateLayeredShortWaveRadiation(ZoneMC, weather.Radn);
                }

            // Light distribution is now complete so calculate remaining micromet equations
            foreach (var ZoneMC in microClimatesZones)
            {
                ZoneMC.CalculateEnergyTerms(soil_albedo);
                ZoneMC.CalculateLongWaveRadiation(dayLengthLight, dayLengthEvap);
                ZoneMC.CalculateSoilHeatRadiation(SoilHeatFluxFraction);
                ZoneMC.CalculateGc(dayLengthEvap);
                ZoneMC.CalculateGa(ReferenceHeight);
                ZoneMC.CalculateInterception(a_interception, b_interception, c_interception, d_interception);
                ZoneMC.CalculatePM(dayLengthEvap, NightInterceptionFraction);
                ZoneMC.CalculateOmega();
                ZoneMC.SetCanopyEnergyTerms();
                ZoneMC.CalculateEo();
            }
        }

        ///<summary> Calculate the short wave radiation balance for strip crop system</summary>
        private void CalculateStripZoneShortWaveRadiation()
        {
            
            MicroClimateZone tallest;
            MicroClimateZone shortest;
            if (MathUtilities.Sum(microClimatesZones[0].DeltaZ)> MathUtilities.Sum(microClimatesZones[1].DeltaZ))
            {
                tallest = microClimatesZones[0];
                shortest = microClimatesZones[1];
            }
            else
            {
                tallest = microClimatesZones[1];
                shortest = microClimatesZones[0];
            }

            bool TallestIsTree = false;
            foreach (MicroClimateCanopy c in tallest.Canopies)
            {
                if ((c.Canopy.Height - c.Canopy.Depth) > 0)
                    TallestIsTree = true;
            }
            if (TallestIsTree)
                DoTreeRowCropShortWaveRadiation(ref tallest, ref shortest);
            else
                DoStripCropShortWaveRadiation(ref tallest, ref shortest);
        }

        /// <summary>
        /// This model is for tree crops where there is no vertical overlap of the shortest and tallest canopy but the tallest canopy can overlap the shortest horzontally
        /// </summary>
        /// <param name="tree"></param>
        /// <param name="alley"></param>
        private void DoTreeRowCropShortWaveRadiation(ref MicroClimateZone tree, ref MicroClimateZone alley)
        {
            if (MathUtilities.Sum(tree.DeltaZ) > 0)  // Don't perform calculations if layers are empty
            {
                double Ht = MathUtilities.Sum(tree.DeltaZ);                // Height of tree canopy
                double CDt = 0;//tree.Canopies[0].Canopy.Depth / 1000;         // Depth of tree canopy
                foreach (MicroClimateCanopy c in tree.Canopies)
                    if (c.Canopy.Depth < c.Canopy.Height)
                    {
                        if (CDt > 0.0)
                            throw new Exception("Can't have two tree canopies");
                        else
                        {
                            CDt = c.Canopy.Depth/1000;
                        }
                    }
                double CBHt = Ht - CDt;                                    // Base hight of the tree canopy
                double Ha = MathUtilities.Sum(alley.DeltaZ);               // Height of alley canopy
                if ((Ha > CBHt) & (tree.DeltaZ.Length > 1))
                    throw (new Exception("Height of the alley canopy must not exceed the base height of the tree canopy"));
                double Wt = (tree.Zone as Zones.RectangularZone).Width;    // Width of tree zone
                double Wa = (alley.Zone as Zones.RectangularZone).Width;   // Width of alley zone
                double CWt = 0;//Math.Min(tree.Canopies[0].Canopy.Width / 1000, (Wt + Wa));// Width of the tree canopy
                foreach (MicroClimateCanopy c in tree.Canopies)
                    if (c.Canopy.Depth < c.Canopy.Height)
                    {
                        if (CWt > 0.0)
                            throw new Exception("Can't have two tree canopies");
                        else
                        {
                            CWt = Math.Min(c.Canopy.Width / 1000,(Wt + Wa));
                        }
                    }
                double WaOl = Math.Min(CWt - Wt, Wa);                         // Width of tree canopy that overlap the alley zone
                double WaOp = Wa - WaOl;                                      // Width of the open alley zone between tree canopies
                double Ft = CWt / (Wt + Wa);                                  // Fraction of space in tree canopy
                double Fs = WaOp / (Wt + Wa);                                 // Fraction of open space in the alley row
                double LAIt = MathUtilities.Sum(tree.LAItotsum);           // LAI of trees
                double LAIa = MathUtilities.Sum(alley.LAItotsum);          // LAI of alley crop
                double Kt = tree.Canopies[0].Ktot;                         // Extinction Coefficient of trees
                double Ka = alley.Canopies[0].Ktot;                        // Extinction Coefficient of alley crop
                double LAIthomo = Ft * LAIt;                                  // LAI of trees if spread homogeneously across row and alley zones
                double Ftbla = (Math.Sqrt(Math.Pow(CDt, 2) + Math.Pow(CWt, 2)) - CDt) / CWt;    // View factor for the tree canopy if a black body
                double Fabla = (Math.Sqrt(Math.Pow(CDt, 2) + Math.Pow(WaOp, 2)) - CDt) / WaOp;  // View factor for the gap between trees in alley if trees a black body
                if (WaOp == 0) Fabla = 0;
                //All transmission and interception values below are a fraction of the total short wave radiation incident to both the tree and alley rows
                double Tt = Ft * (Ftbla * Math.Exp(-Kt * LAIt)
                          + Ft * (1 - Ftbla) * Math.Exp(-Kt * LAIthomo))
                          + Fs * Ft * (1 - Fabla) * Math.Exp(-Kt * LAIthomo);     //  Transmission of light to the bottom of the tree canopy
                double Ta = Fs * (Fabla + Fs * (1 - Fabla) * Math.Exp(-Kt * LAIthomo))
                          + Ft * Fs * ((1 - Ftbla) * Math.Exp(-Kt * LAIthomo));   //  Transmission of light to the bottom of the gap in the tree canopy
                double It = 1 - Tt - Ta;                                    // Interception by the trees
                double St = Tt * Wt / CWt;                                  // Transmission to the soil in the tree zone
                double IaOl = Tt * WaOl / CWt * (1 - Math.Exp(-Ka * LAIa)); // Interception by the alley canopy below the overlap of the trees
                double IaOp = Ta * (1 - Math.Exp(-Ka * LAIa));              // Interception by the alley canopy in the gaps between tree canopy 
                double Ia = IaOl + IaOp;                                    // Interception by the alley canopy
                double SaOl = Tt * WaOl / CWt * (Math.Exp(-Ka * LAIa));     // Transmission to the soil beneigth the alley canopy under the tree canopy
                double SaOp = Ta * (Math.Exp(-Ka * LAIa));                  // Transmission to the soil beneigth the alley canopy in the open
                double Sa = SaOl + SaOp;                                    // Transmission to the soil beneight the alley
                double EnergyBalanceCheck = It + St + Ia + Sa;              // Sum of all light fractions (should equal 1)
                if (Math.Abs(1 - EnergyBalanceCheck) > 0.001)
                    throw (new Exception("Energy Balance not maintained in strip crop light interception model"));

                Ft = (Wt) / (Wt + Wa);  // Remove overlap so scaling back to zone ground area works
                Fs = (Wa) / (Wt + Wa);  // Remove overlap so scaling back to zone ground area works

                CalculateLayeredShortWaveRadiation(tree, weather.Radn * It / Ft);
                
                CalculateLayeredShortWaveRadiation(alley, weather.Radn * Ia / Fs);
            }
            else
            {
                tree.SurfaceRs = weather.Radn;
                CalculateLayeredShortWaveRadiation(alley, weather.Radn);
            }
        }

        /// <summary>
        /// This model is for strip crops where there is verticle overlap of the shortest and tallest crops canopy but no horizontal overlap
        /// </summary>
        /// <param name="tallest"></param>
        /// <param name="shortest"></param>
        private void DoStripCropShortWaveRadiation(ref MicroClimateZone tallest, ref MicroClimateZone shortest)
        {
            if (MathUtilities.Sum(tallest.DeltaZ) > 0)  // Don't perform calculations if layers are empty
            {
                double Ht = MathUtilities.Sum(tallest.DeltaZ);                // Height of tallest strip
                double Hs = MathUtilities.Sum(shortest.DeltaZ);               // Height of shortest strip
                double Wt = (tallest.Zone as Zones.RectangularZone).Width;    // Width of tallest strip
                double Ws = (shortest.Zone as Zones.RectangularZone).Width;   // Width of shortest strip
                double Ft = Wt / (Wt + Ws);                                   // Fraction of space in tallest strip
                double Fs = Ws / (Wt + Ws);                                   // Fraction of space in the shortest strip
                double LAIt = MathUtilities.Sum(tallest.LAItotsum);           // LAI of tallest strip
                double LAIs = MathUtilities.Sum(shortest.LAItotsum);          // LAI of shortest strip
                double Kt = tallest.Canopies[0].Ktot;                         // Extinction Coefficient of the tallest strip
                double Ks = shortest.Canopies[0].Ktot;                        // Extinction Coefficient of the shortest strip
                double Httop = Ht - Hs;                                       // Height of the top layer in tallest strip (ie distance from top of shortest to top of tallest)
                double LAIttop = Httop / Ht * LAIt;                           // LAI of the top layer of the tallest strip (ie LAI in tallest strip above height of shortest strip)
                double LAItbot = LAIt - LAIttop;                              // LAI of the bottom layer of the tallest strip (ie LAI in tallest strip below height of the shortest strip)
                double LAIttophomo = Ft * LAIttop;                            // LAI of top layer of tallest strip if spread homogeneously across all of the space
                double Ftblack = (Math.Sqrt(Math.Pow(Httop, 2) + Math.Pow(Wt, 2)) - Httop) / Wt;  // View factor for top layer of tallest strip
                double Fsblack = (Math.Sqrt(Math.Pow(Httop, 2) + Math.Pow(Ws, 2)) - Httop) / Ws;  // View factor for top layer of shortest strip
                double Tt = Ft * (Ftblack * Math.Exp(-Kt * LAIttop)
                          + Ft * (1 - Ftblack) * Math.Exp(-Kt * LAIttophomo))
                          + Fs * Ft * (1 - Fsblack) * Math.Exp(-Kt * LAIttophomo);  //  Transmission of light to bottom of top layer in tallest strip
                double Ts = Fs * (Fsblack + Fs * (1 - Fsblack) * Math.Exp(-Kt * LAIttophomo))
                          + Ft * Fs * ((1 - Ftblack) * Math.Exp(-Kt * LAIttophomo));           //  Transmission of light to bottom of top layer in shortest strip
                double Intttop = 1 - Tt - Ts;                                 // Interception by the top layer of the tallest strip (ie light intercepted in tallest strip above height of shortest strip)
                double Inttbot = (Tt * (1 - Math.Exp(-Kt * LAItbot)));        // Interception by the bottom layer of the tallest strip
                double Soilt = (Tt * (Math.Exp(-Kt * LAItbot)));              // Transmission to the soil below tallest strip
                double Ints = Ts * (1 - Math.Exp(-Ks * LAIs));                // Interception by the shortest strip
                double Soils = Ts * (Math.Exp(-Ks * LAIs));                   // Transmission to the soil below shortest strip
                double EnergyBalanceCheck = Intttop + Inttbot + Soilt + Ints + Soils;  // Sum of all light fractions (should equal 1)
                if (Math.Abs(1 - EnergyBalanceCheck) > 0.001)
                    throw (new Exception("Energy Balance not maintained in strip crop light interception model"));

                //tallest.Canopies[0].Rs[0] = weather.Radn * (Intttop + Inttbot) / Ft;
                //tallest.SurfaceRs = weather.Radn * Soilt / Ft;
                CalculateLayeredShortWaveRadiation(tallest, weather.Radn * (Intttop + Inttbot) / Ft);

               // if (shortest.Canopies[0].Rs != null)
               //     if (shortest.Canopies[0].Rs.Length > 0)
               //         shortest.Canopies[0].Rs[0] = weather.Radn * Ints / Fs;
                //shortest.SurfaceRs = weather.Radn * Soils / Fs;
                CalculateLayeredShortWaveRadiation(shortest, weather.Radn * Ints / Fs);
            }
            else
            {
                //tallest.Canopies[0].Rs[0] =0;
                tallest.SurfaceRs = weather.Radn;
                //shortest.Canopies[0].Rs[0] = 0;
                shortest.SurfaceRs = weather.Radn;
            }


        }

        /// <summary>
        /// Calculates interception of short wave by canopy compartments
        /// </summary>
        private void CalculateLayeredShortWaveRadiation(MicroClimateZone ZoneMC, double Rin)
        {
            // Perform Top-Down Light Balance
            // ==============================
            double Rint = 0;
            for (int i = ZoneMC.numLayers - 1; i >= 0; i += -1)
            {
                if (double.IsNaN(Rint))
                    throw new Exception("Bad Radiation Value in Light partitioning");
                Rint = Rin * (1.0 - Math.Exp(-ZoneMC.layerKtot[i] * ZoneMC.LAItotsum[i]));
                for (int j = 0; j <= ZoneMC.Canopies.Count - 1; j++)
                    ZoneMC.Canopies[j].Rs[i] = Rint * MathUtilities.Divide(ZoneMC.Canopies[j].Ftot[i] * ZoneMC.Canopies[j].Ktot, ZoneMC.layerKtot[i], 0.0);
                Rin -= Rint;
            }
            ZoneMC.SurfaceRs = Rin;
        }
    }
}