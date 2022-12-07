﻿using System;
using Models.Core;
using Models.DCAPST.Interfaces;

namespace Models.DCAPST
{
    /// <summary>
    /// Implements the canopy parameters
    /// </summary>
    [Serializable]
    public class CanopyParameters : ICanopyParameters
    {
        /// <summary>
        /// Canopy type.
        /// </summary>
        [Description("Canopy Type")]
        public CanopyType Type { get; set; } = CanopyType.C4;

        /// <summary>
        /// Partial pressure of O2 in air.
        /// </summary>
        [Description("Partial pressure of O2 in air")]
        [Units("μbar")]
        public double AirO2 { get; set; } = 210000;

        /// <summary>
        /// Partial pressure of CO2 in air
        /// </summary>
        [Description("Partial pressure of CO2 in air")]
        [Units("μbar")]
        public double AirCO2 { get; set; } = 363;

        /// <summary>
        /// Canopy average leaf inclination relative to the horizontal (degrees)
        /// </summary>
        [Description("Average leaf angle (relative to horizontal)")]
        [Units("Degrees")]
        public double LeafAngle { get; set; } = 60;

        /// <summary>
        /// The leaf width in the canopy
        /// </summary>
        [Description("Average leaf width")]
        [Units("")]
        public double LeafWidth { get; set; } = 0.15;

        /// <summary>
        /// Leaf-level coefficient of scattering radiation
        /// </summary>
        [Description("Leaf-level coefficient of scattering radiation")]
        [Units("")]
        public double LeafScatteringCoeff { get; set; } = 0.15;

        /// <summary>
        /// Leaf-level coefficient of near-infrared scattering radiation
        /// </summary>
        [Description("Leaf-level coefficient of scattering NIR")]
        [Units("")]
        public double LeafScatteringCoeffNIR { get; set; } = 0.8;

        /// <summary>
        /// Extinction coefficient for diffuse radiation
        /// </summary>
        [Description("Diffuse radiation extinction coefficient")]
        [Units("")]
        public double DiffuseExtCoeff { get; set; } = 0.78;

        /// <summary>
        /// Extinction coefficient for near-infrared diffuse radiation
        /// </summary>
        [Description("Diffuse NIR extinction coefficient")]
        [Units("")]
        public double DiffuseExtCoeffNIR { get; set; } = 0.8;

        /// <summary>
        /// Reflection coefficient for diffuse radiation
        /// </summary>
        [Description("Diffuse radiation reflection coefficient")]
        [Units("")]
        public double DiffuseReflectionCoeff { get; set; } = 0.036;

        /// <summary>
        /// Reflection coefficient for near-infrared diffuse radiation
        /// </summary>
        [Description("Diffuse NIR reflection coefficient")]
        [Units("")]
        public double DiffuseReflectionCoeffNIR { get; set; } = 0.389;

        /// <summary>
        /// Local wind speed
        /// </summary>
        [Description("Local wind speed")]
        [Units("")]
        public double Windspeed { get; set; } = 1.5;

        /// <summary>
        /// Extinction coefficient for local wind speed
        /// </summary>
        [Description("Wind speed extinction coefficient")]
        [Units("")]
        public double WindSpeedExtinction { get; set; } = 1.5;

        /// <summary>
        /// Empirical curvature factor
        /// </summary>
        [Description("Empirical curvature factor")]
        [Units("")]
        public double CurvatureFactor { get; set; } = 0.7;

        /// <inheritdoc />
        [Description("Diffusivity solubility ratio")]
        [Units("")]
        public double DiffusivitySolubilityRatio { get; set; } = 0.047;

        /// <summary>
        /// The minimum nitrogen value at or below which CO2 assimilation rate is zero (mmol N m^-2)
        /// </summary>
        [Description("Minimum nitrogen for assimilation")]
        [Units("")]
        public double MinimumN { get; set; } = 14;

        /// <summary>
        /// Ratio of the average canopy specific leaf nitrogen (SLN) to the SLN at the top of canopy (g N m^-2 leaf)
        /// </summary>
        [Description("Ratio of average SLN to canopy top SLN")]
        [Units("")]
        public double SLNRatioTop { get; set; } = 1.3;
    }
}
