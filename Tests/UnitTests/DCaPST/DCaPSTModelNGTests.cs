﻿using GLib;
using Models.DCAPST;
using Models.DCAPST.Interfaces;
using Moq;
using NUnit.Framework;

namespace UnitTests.DCaPST
{
    [TestFixture]
    public class DCaPSTModelNGTests
    {
        #region Tests

        [Test]
        public void SetCropName_NewValue_HandleCropChangeCalled()
        {
            // Arrange
            var cropName = "testCrop";
            var mock = new Mock<ICropParameterGenerator>(MockBehavior.Strict);
            mock.Setup(cropGen => cropGen.Generate(It.IsAny<string>())).Returns(new DCaPSTParameters()).Verifiable();

            var model = new DCaPSTModelNG();
            DCaPSTModelNG.ParameterGenerator = mock.Object;

            // Act
            model.CropName = cropName;

            // Assert
            mock.Verify(cropGen => cropGen.Generate(cropName), Times.Once());
            Assert.AreEqual(model.CropName, cropName);
        }

        [Test]
        public void SetCropName_SameValue_HandleCropChangeCalled()
        {
            // Arrange
            var cropName = "testCrop";
            var mock = new Mock<ICropParameterGenerator>(MockBehavior.Strict);
            mock.Setup(cropGen => cropGen.Generate(It.IsAny<string>())).Returns(new DCaPSTParameters()).Verifiable();

            var model = new DCaPSTModelNG();
            DCaPSTModelNG.ParameterGenerator = mock.Object;

            // Act
            model.CropName = cropName;
            model.CropName = cropName;

            // Assert
            mock.Verify(cropGen => cropGen.Generate(cropName), Times.Once());
            Assert.AreEqual(model.CropName, cropName);
        }

        [Test]
        public void SetCropName_DifferentValue_HandleCropChangeCalled()
        {
            // Arrange
            var cropName = "testCrop";
            var differentCropName = $"Different-{cropName}";
            var mock = new Mock<ICropParameterGenerator>(MockBehavior.Strict);
            mock.Setup(cropGen => cropGen.Generate(It.IsAny<string>())).Returns(new DCaPSTParameters()).Verifiable();

            var model = new DCaPSTModelNG();
            DCaPSTModelNG.ParameterGenerator = mock.Object;

            // Act
            model.CropName = cropName;
            model.CropName = differentCropName;

            // Assert
            mock.Verify(cropGen => cropGen.Generate(cropName), Times.Once());
            mock.Verify(cropGen => cropGen.Generate(differentCropName), Times.Once());
            Assert.AreEqual(model.CropName, differentCropName);
        }

        [Test]
        public void SetParameters_ValueSet()
        {
            // Arrange
            // Choose a few random params to test.
            var airC02 = 55.5;
            var atpProductionElectronTransportFactor = 1.043;
            var rpar = 20.7;

            var dcapstParameters = new DCaPSTParameters()
            {
                Canopy = new CanopyParameters()
                {
                    AirCO2 = airC02
                },
                Pathway = new PathwayParameters()
                {
                    ATPProductionElectronTransportFactor = atpProductionElectronTransportFactor
                },
                Rpar = rpar
            };

            var model = new DCaPSTModelNG
            {
                // Act
                Parameters = dcapstParameters
            };

            // Assert
            Assert.AreEqual(model.Parameters.Canopy.AirCO2, airC02);
            Assert.AreEqual(model.Parameters.Pathway.ATPProductionElectronTransportFactor, atpProductionElectronTransportFactor);
            Assert.AreEqual(model.Parameters.Rpar, rpar);
        }

        [Test]
        public void SetupModel_ValueSet()
        {
            // Arrange
            var canopyParameters = new CanopyParameters();
            var pathwayParameters = new PathwayParameters();
            var dayOfYear = DateTime.NewNowUtc().DayOfYear;
            var latitude = 50.7220;
            var maxT = 30.0;
            var minT = -10.0;
            var radn = 1.0;
            var rpar = 2.0;

            // Act
            var model = DCaPSTModelNG.SetUpModel(
                canopyParameters, 
                pathwayParameters, 
                dayOfYear, 
                latitude, 
                maxT, 
                minT, 
                radn, 
                rpar
            );

            // Assert - Nothing else can be tested :-(
            Assert.AreEqual(model.B, 0.409);
        }

        #endregion
    }
}
