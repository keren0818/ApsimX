﻿using APSIM.Shared.Utilities;
using Models;
using Models.Core;
using NUnit.Framework;
using System;
using APSIM.Shared.Documentation;
using Models.Core.ApsimFile;
using Models.Core.Run;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnitTests.Storage;
using Shared.Utilities;

namespace UnitTests.ManagerTests
{

    /// <summary>
    /// Unit Tests for manager scripts.
    /// </summary>
    class ManagerTests
    {
        /// <summary>Flags required for reflection that gets all public and private methods </summary>
        private const BindingFlags reflectionFlagsMethods = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.InvokeMethod;
        /// <summary>Flags required for reflection to gets all public properties </summary>
        private const BindingFlags reflectionFlagsProperties = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.InvokeMethod;

        /// <summary>
        /// Creates a Manager with different levels of setup for the unit tests to run with.
        /// </summary>
        private Manager createManager(bool withParent, bool withCompiler, bool withCode, bool withAlreadyCompiled)
        {
            Simulations sims = new Simulations();
            ScriptCompiler compiler = new ScriptCompiler();
            Manager testManager = new Manager();

            if (withParent)
                testManager.Parent = sims;

            if (withCompiler)
                typeof(Manager).InvokeMember("SetCompiler", reflectionFlagsMethods, null, testManager, new object[] { compiler });         

            string basicCode = "";
            basicCode += "using System.Linq;\n";
            basicCode += "using System;\n";
            basicCode += "using Models.Core;\n";
            basicCode += "namespace Models {\n";
            basicCode += "\t[Serializable]\n";
            basicCode += "\tpublic class Script : Model {\n";
            basicCode += "\t\t[Description(\"AProperty\")]\n";
            basicCode += "\t\tpublic string AProperty { get; set; } = \"Hello World\";\n";
            basicCode += "\t\t\tpublic void Document() { return; }\n";
            basicCode += "\t}\n";
            basicCode += "}\n";

            if (withCode)
                testManager.Code = basicCode;

            if (withAlreadyCompiled)
            {
                if ((!withParent && !withCompiler) || !withCode)
                {
                    throw new Exception("Cannot create test Manager withAlreadyCompiled without a compiler and withGoodCode");
                }
                else
                {
                    testManager.OnCreated();
                    testManager.GetParametersFromScriptModel();
                }
            }
                

            return testManager;
        }


        /// <summary>
        /// This test reproduces a bug in which a simulation could run without
        /// error despite a manager script containing a syntax error.
        /// </summary>
        [Test]
        public void TestManagerWithError()
        {
            var simulations = new Simulations()
            { 
                Children = new List<IModel>()
                {
                    new Simulation()
                    {
                        Name = "Sim",
                        FileName = Path.GetTempFileName(),
                        Children = new List<IModel>()
                        {
                            new Clock()
                            {
                                StartDate = new DateTime(2019, 1, 1),
                                EndDate = new DateTime(2019, 1, 2)
                            },
                            new MockSummary(),
                            new Manager()
                            {
                                Code = "asdf"
                            }
                        }
                    }
                }
            };

            var runner = new Runner(simulations);
            Assert.IsNotNull(runner.Run());
        }

        /// <summary>
        /// This test ensures that scripts aren't recompiled after events have
        /// been hooked up. Such behaviour would cause scripts to not receive
        /// any events, and the old/discarded scripts would receive events.
        /// </summary>
        [Test]
        public void TestScriptNotRebuilt()
        {
            string json = ReflectionUtilities.GetResourceAsString("UnitTests.bork.apsimx");
            IModel file = FileFormat.ReadFromString<IModel>(json, e => throw e, false).NewModel as IModel;
            Simulation sim = file.FindInScope<Simulation>();
            Assert.DoesNotThrow(() => sim.Run());
        }

        /// <summary>
        /// Ensures that Manager Scripts are allowed to override the
        /// OnCreated() method.
        /// </summary>
        /// <remarks>
        /// OnCreatedError.apsimx contains a manager script which overrides
        /// the OnCreated() method and throws an exception from this method.
        /// 
        /// This test ensures that an exception is thrown and that it is the
        /// correct exception.
        /// 
        /// The manager in this file is disabled, but its OnCreated() method
        /// should still be called.
        /// </remarks>
        [Test]
        public void ManagerScriptOnCreated()
        {
            string json = ReflectionUtilities.GetResourceAsString("UnitTests.Core.ApsimFile.OnCreatedError.apsimx");
            List<Exception> errors = new List<Exception>();
            FileFormat.ReadFromString<IModel>(json, e => errors.Add(e), false);

            Assert.NotNull(errors);
            Assert.AreEqual(1, errors.Count, "Encountered the wrong number of errors when opening OnCreatedError.apsimx.");
            Assert.That(errors[0].ToString().Contains("Error thrown from manager script's OnCreated()"), "Encountered an error while opening OnCreatedError.apsimx, but it appears to be the wrong error: {0}.", errors[0].ToString());
        }

        /// <summary>
        /// Reproduces issue #5202. This appears to be due to a bug where manager script parameters are not being 
        /// correctly overwritten by factors of an experiment (more precisely, they are overwritten, and then the 
        /// overwritten values are themselves being overwritten by the original values).
        /// </summary>
        [Test]
        public void TestManagerOverrides()
        {
            string json = ReflectionUtilities.GetResourceAsString("UnitTests.Manager.ManagerOverrides.apsimx");
            Simulations sims = FileFormat.ReadFromString<Simulations>(json, e => throw e, false).NewModel as Simulations;

            foreach (Runner.RunTypeEnum runType in Enum.GetValues(typeof(Runner.RunTypeEnum)))
            {
                Runner runner = new Runner(sims);
                List<Exception> errors = runner.Run();
                if (errors != null && errors.Count > 0)
                    throw errors[0];
            }
        }

        /// <summary>
        /// This test ensures one manager model can call another.
        /// </summary>
        [Test]
        public void TestOneManagerCallingAnother()
        {
            var simulations = new Simulations()
            { 
                Children = new List<IModel>()
                {
                    new Simulation()
                    {
                        Children = new List<IModel>()
                        {
                            new Clock() { StartDate = new DateTime(2020, 1, 1), EndDate = new DateTime(2020, 1, 1)},
                            new MockSummary(),
                            new MockStorage(),
                            new Manager()
                            {
                                Name = "Manager1",
                                Code = "using Models.Core;" + Environment.NewLine +
                                       "using System;" + Environment.NewLine +
                                       "namespace Models" + Environment.NewLine +
                                       "{" + Environment.NewLine +
                                       "    [Serializable]" + Environment.NewLine +
                                       "    public class Script1 : Model" + Environment.NewLine +
                                       "    {" + Environment.NewLine +
                                       "        public int A = 1;" + Environment.NewLine +
                                       "    }" + Environment.NewLine +
                                       "}"
                            },
                            new Manager()
                            {
                                Name = "Manager2",
                                Code = "using Models.Core;" + Environment.NewLine +
                                       "using System;" + Environment.NewLine +
                                       "namespace Models" + Environment.NewLine +
                                       "{" + Environment.NewLine +
                                       "    [Serializable]" + Environment.NewLine +
                                       "    public class Script2 : Model" + Environment.NewLine +
                                       "    {" + Environment.NewLine +
                                       "        [Link] Script1 otherScript;" + Environment.NewLine +
                                       "        public int B { get { return otherScript.A + 1; } }" + Environment.NewLine +
                                       "    }" + Environment.NewLine +
                                       "}"
                            },
                            new Models.Report()
                            {
                                VariableNames = new string[] { "[Script2].B" },
                                EventNames = new string[] { "[Clock].EndOfDay" }
                            }
                        }
                    }
                }
            };
            //Apsim.InitialiseModel(simulations);

            var storage = simulations.Children[0].Children[2] as MockStorage;

            var runner = new Runner(simulations);
            runner.Run();

            double[] actual = storage.Get<double>("[Script2].B");
            double[] expected = new double[] { 2 };
            Assert.AreNotEqual(expected, actual);
        }

        /// <summary>
        /// Specific test for SetCompiler and Compiler
        /// These two work together, so should be tested together.
        /// Should not throw when a compiler is attached to a blank manager using these methods
        /// </summary>
        [Test]
        public void SetCompilerAndCompilerTests()
        {
            ScriptCompiler compiler = new ScriptCompiler();
            Manager testManager = new Manager();

            typeof(Manager).InvokeMember("SetCompiler", reflectionFlagsMethods, null, testManager, new object[] { compiler });
            Assert.DoesNotThrow(() => typeof(Manager).InvokeMember("Compiler", reflectionFlagsMethods, null, testManager, null));
        }

        /// <summary>
        /// Specific test for TryGetCompiler
        /// Should return false on an empty Manager
        /// Should return true if has an ancestor of Simulations (since it loads a compiler)
        /// Should return true if it has the compiler directly attached
        /// </summary>
        [Test]
        public void TryGetCompilerTests()
        {
            Manager testManager;

            //Should be false if running without a compiler
            testManager = createManager(false, false, true, false);
            Assert.False((bool)typeof(Manager).InvokeMember("TryGetCompiler", reflectionFlagsMethods, null, testManager, null));

            //should be found in sims
            testManager = createManager(true, false, true, false);
            Assert.True((bool)typeof(Manager).InvokeMember("TryGetCompiler", reflectionFlagsMethods, null, testManager, null));

            //check if works assigning directly.
            testManager = createManager(false, true, true, false);
            ScriptCompiler compiler = new ScriptCompiler();
            typeof(Manager).InvokeMember("SetCompiler", reflectionFlagsMethods, null, testManager, new object[] { compiler });
            Assert.True((bool)typeof(Manager).InvokeMember("TryGetCompiler", reflectionFlagsMethods, null, testManager, null));
        }

        /// <summary>
        /// Specific test for OnStartOfSimulation
        /// Should do nothing if using empty Manager that has a parent
        /// Should compile and have parameters if setup fully and compiled
        /// Should fail if after compiling, the code is changed to broken code and compiled again
        /// </summary>
        [Test]
        public void OnStartOfSimulationTests()
        {
            Manager testManager;

            //should not throw, but not do anything
            testManager = createManager(true, false, true, false);
            Assert.DoesNotThrow(() => typeof(Manager).InvokeMember("OnStartOfSimulation", reflectionFlagsMethods, null, testManager, new object[] { new object(), new EventArgs() }));
            Assert.IsNull(testManager.Parameters);

            //should work
            testManager = createManager(true, false, true, true);
            Assert.DoesNotThrow(() => typeof(Manager).InvokeMember("OnStartOfSimulation", reflectionFlagsMethods, null, testManager, new object[] { new object(), new EventArgs() }));
            Assert.AreEqual(1, testManager.Parameters.Count);

            //Should fail, even though previously compiled with code.
            testManager = createManager(true, false, true, true);
            Assert.Throws<Exception>(() => testManager.Code = testManager.Code.Replace('{', 'i'));
            Assert.Throws<TargetInvocationException>(() => typeof(Manager).InvokeMember("OnStartOfSimulation", reflectionFlagsMethods, null, testManager, new object[] { new object(), new EventArgs() }));
        }

        /// <summary>
        /// Specific test for SetParametersInScriptModel
        /// Should not do anything or error on a blank manager
        /// Should make parameters if fully set up
        /// Should not have parameters if compiled but disabled
        /// </summary>
        [Test]
        public void SetParametersInScriptModelTests()
        {
            Manager testManager;

            //Should not throw, but not make parameters
            testManager = createManager(false, false, false, false);
            Assert.DoesNotThrow(() => typeof(Manager).InvokeMember("SetParametersInScriptModel", reflectionFlagsMethods, null, testManager, new object[] { }));
            Assert.IsNull(testManager.Parameters);

            //Should make parameters
            testManager = createManager(false, true, true, true);
            Assert.DoesNotThrow(() => typeof(Manager).InvokeMember("SetParametersInScriptModel", reflectionFlagsMethods, null, testManager, new object[] { }));
            Assert.AreEqual(1, testManager.Parameters.Count);

            //Should not make parameters
            testManager = createManager(false, true, true, false);
            testManager.Enabled = false;
            testManager.OnCreated();
            testManager.GetParametersFromScriptModel();
            Assert.DoesNotThrow(() => typeof(Manager).InvokeMember("SetParametersInScriptModel", reflectionFlagsMethods, null, testManager, new object[] { }));
            Assert.IsNull(testManager.Parameters);
        }

        /// <summary>
        /// Specific test for GetParametersFromScriptModel
        /// Should not do anything or error on a blank manager
        /// Should make parameters if script compiled
        /// </summary>
        [Test]
        public void GetParametersFromScriptModelTests()
        {
            Manager testManager;

            //Should not throw, but not make parameters
            testManager = createManager(false, false, false, false);
            Assert.DoesNotThrow(() => testManager.GetParametersFromScriptModel());
            Assert.IsNull(testManager.Parameters);

            //Should make parameters
            testManager = createManager(false, true, true, false);
            testManager.OnCreated();
            Assert.DoesNotThrow(() => testManager.GetParametersFromScriptModel());
            Assert.AreEqual(1, testManager.Parameters.Count);
        }

        /// <summary>
        /// Specific test for OnCreated
        /// Should not do anything or error on a blank manager
        /// Should not do anything or error on a manager with no script
        /// Should compile the script and allow parameteres to be made if has compiler and code
        /// </summary>
        [Test]
        public void OnCreatedTests()
        {
            Manager testManager;

            //shouldn't throw, but shouldn't load any script
            testManager = createManager(false, false, false, false);
            Assert.DoesNotThrow(() => testManager.OnCreated());
            Assert.IsNull(testManager.Parameters);

            //shouldn't throw, but shouldn't load any script
            testManager = createManager(true, false, false, false);
            Assert.DoesNotThrow(() => testManager.OnCreated());
            Assert.DoesNotThrow(() => testManager.GetParametersFromScriptModel());
            Assert.AreEqual(0, testManager.Parameters.Count);

            //should compile the script
            testManager = createManager(true, false, true, false);
            Assert.DoesNotThrow(() => testManager.OnCreated());
            Assert.DoesNotThrow(() => testManager.GetParametersFromScriptModel());
            Assert.AreEqual(1, testManager.Parameters.Count);
        }

        /// <summary>
        /// Specific test for RebuildScriptModel
        /// Tests a bunch of different inputs for scripts to make sure the compile under different setups
        /// </summary>
        [Test]
        public void RebuildScriptModelTests()
        {
            Manager testManager;

            //should not throw, but should not compile when Oncreated has been run
            testManager = createManager(true, false, true, false);
            Assert.DoesNotThrow(() => testManager.RebuildScriptModel());
            Assert.IsNull(testManager.Parameters);

            //should compile and have parameters
            testManager = createManager(true, false, true, true);
            Assert.DoesNotThrow(() => testManager.RebuildScriptModel());
            Assert.AreEqual(1, testManager.Parameters.Count);

            //should not compile if not enabled
            testManager = createManager(true, false, true, false);
            testManager.Enabled = false;
            testManager.OnCreated();
            Assert.DoesNotThrow(() => testManager.RebuildScriptModel());
            Assert.IsNull(testManager.Parameters);

            //should not compile if not code, but with oncreated run.
            testManager = createManager(true, false, false, false);
            testManager.OnCreated();
            Assert.DoesNotThrow(() => testManager.RebuildScriptModel());
            Assert.AreEqual(0, testManager.Parameters.Count);

            //should not compile if code is empty
            testManager = createManager(true, false, false, false);
            testManager.Code = "";
            testManager.OnCreated();
            Assert.DoesNotThrow(() => testManager.RebuildScriptModel());
            Assert.IsNull(testManager.Parameters);

            //should throw error if broken code
            testManager = createManager(true, false, true, false);
            testManager.Code = testManager.Code.Replace("{", "");
            Assert.Throws<Exception>(() => testManager.OnCreated());
            Assert.Throws<Exception>(() => testManager.RebuildScriptModel());
            Assert.IsNull(testManager.Parameters);
        }

        /// <summary>
        /// Specific test for Document
        /// Document should make a document object with the parameters listed
        /// It should fail if paramters have not been generated, even if code is there.
        /// </summary>
        [Test]
        public void DocumentTests()
        {
            Manager testManager;

            //should work
            testManager = createManager(true, false, true, true);
            List<ITag> tags = new List<ITag>();
            foreach (ITag tag in testManager.Document())
                tags.Add(tag);
            Assert.AreEqual(1, tags.Count);

            //should not work
            testManager = createManager(true, false, true, false);
            tags = new List<ITag>();
            foreach (ITag tag in testManager.Document())
                tags.Add(tag);
            Assert.AreEqual(0, tags.Count);
        }

        /// <summary>
        /// Specific test for CodeArray
        /// Put code into the code array property, then pull it out and check that what you have
        /// is the same as what is stored
        /// </summary>
        [Test]
        public void CodeArrayTests()
        {
            Manager testManager;

            testManager = createManager(false, false, true, false);

            string[] array = testManager.CodeArray;
            testManager.CodeArray = array;

            string[] array2 = testManager.CodeArray;
            Assert.AreEqual(array, array2);
        }

        /// <summary>
        /// Specific test for Code
        /// Check a range of inputs that could be stored in the code property
        /// Then pull those inputs out and make sure they are the same
        /// </summary>
        [Test]
        public void CodeTests()
        {
            Manager testManager;

            testManager = createManager(false, false, true, false);

            //empty
            testManager = new Manager();
            testManager.Code = "";
            Assert.AreEqual("", testManager.Code);

            //one space
            testManager = new Manager();
            testManager.Code = " ";
            Assert.AreEqual(" ", testManager.Code);

            //two lines
            testManager = new Manager();
            testManager.Code = " \n ";
            Assert.AreEqual(" \n ", testManager.Code);

            //null - should throw
            testManager = new Manager();
            Assert.Throws<Exception>(() => testManager.Code = null);

            //code in and out
            string code = createManager(false, false, true, false).Code;
            testManager = new Manager();
            testManager.Code = code;
            Assert.AreEqual(testManager.Code, code);

            //should remove \r characters
            string codeWithR = code.Replace("\n", "\r\n");
            testManager = new Manager();
            testManager.Code = codeWithR;
            Assert.AreNotEqual(codeWithR, testManager.Code);

            //should compile
            testManager = createManager(true, false, true, false);
            testManager.OnCreated();
            testManager.GetParametersFromScriptModel();
            Assert.AreEqual(1, testManager.Parameters.Count);
        }

        /// <summary>
        /// Specific test for Parameters
        /// Check that inputing parameters are stored the same
        /// Check that null clears the parameters
        /// </summary>
        [Test]
        public void ParametersTests()
        {
            Manager testManager;

            List<KeyValuePair<string, string>> paras = new List<KeyValuePair<string, string>>();
            paras.Add(new KeyValuePair<string, string>("AProperty", "Hello World"));

            testManager = new Manager();
            testManager.Parameters = paras;
            Assert.AreEqual(paras, testManager.Parameters);

            testManager = new Manager();
            testManager.Parameters = null;
            Assert.IsNull(testManager.Parameters);
        }

        /// <summary>
        /// Specific test for Cursor
        /// Check that the cursor values are stored
        /// Check that it is set to null when nulled
        /// </summary>
        [Test]
        public void CursorTests()
        {
            Manager testManager;

            testManager = new Manager();
            ManagerCursorLocation loc = new ManagerCursorLocation();
            loc.TabIndex = 1;
            testManager.Cursor = loc;
            Assert.AreEqual(loc, testManager.Cursor);

            testManager = new Manager();
            testManager.Cursor = null;
            Assert.IsNull(testManager.Cursor);
        }

        /// <summary>
        /// A test to check that all functions in Manager have been tested by a unit test.
        /// </summary>
        [Test]
        public void MethodsHaveUnitTests()
        {
            //Get list of methods in this test file
            List<MethodInfo> testMethods = ReflectionUtilities.GetAllMethods(typeof(ManagerTests), reflectionFlagsMethods, false);
            string names = "";
            foreach (MethodInfo method in testMethods)
                names += method.Name + "\n";

            //Get lists of methods and properties from Manager
            List<MethodInfo> methods = ReflectionUtilities.GetAllMethodsWithoutProperties(typeof(Manager));
            List<PropertyInfo> properties = ReflectionUtilities.GetAllProperties(typeof(Manager), reflectionFlagsProperties, false);

            //Check that at least one of the methods is named for the method or property
            foreach (MethodInfo method in methods)
                if (names.Contains(method.Name) == false)
                    Assert.Fail($"{method.Name} is not tested by an individual unit test.");

            foreach (PropertyInfo prop in properties)
                if (names.Contains(prop.Name) == false)
                    Assert.Fail($"{prop.Name} is not tested by an individual unit test.");
        }
    }
}
