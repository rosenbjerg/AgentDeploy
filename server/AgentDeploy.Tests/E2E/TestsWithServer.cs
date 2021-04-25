using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AgentDeploy.ExternalApi;
using AgentDeploy.Models.Scripts;
using AgentDeploy.Models.Tokens;
using AgentDeploy.Services;
using AgentDeploy.Services.Scripts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using NUnit.Framework;

namespace AgentDeploy.Tests.E2E
{
    [Category("E2E")]
    public class TestsWithServer
    {
        private IHost _host = null!;

        [OneTimeSetUp]
        public async Task StartServer()
        {
            var server = Program.CreateHostBuilder<TestApiStartup>(Array.Empty<string>());
            _host = await server.StartAsync();
        }

        [OneTimeTearDown]
        public async Task StopServer()
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        [TearDown]
        public void Reset()
        {
            _host.Services.GetRequiredService<Mock<ITokenReader>>().Reset();
            _host.Services.GetRequiredService<Mock<IScriptReader>>().Reset();
        }
        
        [Test]
        public async Task InvalidToken()
        {
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke script http://localhost:5000 -t test");
            Assert.NotZero(exitCode);
            Assert.AreEqual("error: token is invalid", instance.ErrorData[0]);
        }
        
        [Test]
        public async Task MissingScriptAccess()
        {
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration>() });
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test");
            
            Assert.NotZero(exitCode);
            Assert.AreEqual("error: script 'test' not found", instance.ErrorData[0]);
        }

        [Test]
        public async Task ImplicitScriptAccess_ScriptNotFound()
        {
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test");
            
            Assert.NotZero(exitCode);
            Assert.AreEqual("error: script 'test' not found", instance.ErrorData[0]);
        }

        [Test]
        public async Task ExplicitScriptAccess_ScriptNotFound()
        {
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test");
            
            Assert.NotZero(exitCode);
            Assert.AreEqual("error: script 'test' not found", instance.ErrorData[0]);
        }

        [Test]
        public async Task ImplicitScriptAccess_ScriptExists()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test");
            
            Assert.Zero(exitCode);
            Assert.IsTrue(instance.OutputData[1].EndsWith("testing-123"));
        }
        
        [Test]
        public async Task ExplicitScriptAccess_ScriptExists()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test");
            
            Assert.Zero(exitCode);
            Assert.IsTrue(instance.OutputData[1].EndsWith("testing-123"));
        }

        [Test]
        public async Task Websocket_Output()
        {
            It.IsAny<string>();
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --ws");
            
            Assert.Zero(exitCode);
            Assert.IsTrue(instance.OutputData[1].EndsWith("testing-123"));
        }
        
        [TestCase("test1", "test1", "tok1", "tok1", ConcurrentExecutionLevel.Full, true)]
        [TestCase("test1", "test1", "tok1", "tok1", ConcurrentExecutionLevel.None, false)]
        [TestCase("test1", "test1", "tok1", "tok1", ConcurrentExecutionLevel.PerToken, false)]
        
        [TestCase("test1", "test1", "tok1", "tok2", ConcurrentExecutionLevel.None, false)]
        [TestCase("test1", "test1", "tok1", "tok2", ConcurrentExecutionLevel.PerToken, true)]

        [TestCase("test1", "test2", "tok1", "tok2", ConcurrentExecutionLevel.None, true)]
        [TestCase("test1", "test2", "tok1", "tok2", ConcurrentExecutionLevel.PerToken, true)]
        public async Task Locking(string scriptName1, string scriptName2, string token1, string token2, ConcurrentExecutionLevel concurrencyLevel, bool success)
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load(scriptName1)).ReturnsAsync(new Script { Command = "sleep 1", Concurrency = concurrencyLevel, Name = scriptName1 });
            if (scriptName1 != scriptName2)
                scriptReaderMock.Setup(s => s.Load(scriptName2)).ReturnsAsync(new Script { Command = "sleep 1", Concurrency = concurrencyLevel, Name = scriptName2 });
            
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile(token1, It.IsAny<CancellationToken>())).ReturnsAsync(new Token());
            if (token1 != token2) 
                tokenReaderMock.Setup(s => s.ParseTokenFile(token2, It.IsAny<CancellationToken>())).ReturnsAsync(new Token());
            
            var task1 = E2ETestUtils.ClientOutput($"invoke {scriptName1} http://localhost:5000 -t {token1}");
            await Task.Delay(50);
            var task2 = E2ETestUtils.ClientOutput($"invoke {scriptName2} http://localhost:5000 -t {token2}");

            var result = await Task.WhenAll(task1, task2);
            var task1Result = result[0];
            var task2Result = result[1];
            
            Assert.Zero(task1Result.exitCode);
            if (success)
            {
                Assert.Zero(task2Result.exitCode);
            }
            else
            {
                Assert.NotZero(task2Result.exitCode);
                Assert.AreEqual($"error: The script '{scriptName2}' is currently locked. Try again later", task2Result.instance.ErrorData[0]);
            }
        }
        
        [TestCase("127.0.0.1", true)]
        [TestCase("127.0.0.1-127.0.0.10", true)]
        [TestCase("128.0.0.1", false)]
        [TestCase("128.0.0.1-128.0.0.10", false)]
        public async Task TrustedIpFilter(string trustedIp, bool success)
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { TrustedIps = new List<string>{ trustedIp }, AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test");

            if (success)
            {
                Assert.Zero(exitCode);
                Assert.IsTrue(instance.OutputData[1].EndsWith("testing-123"));
            }
            else
            {
                Assert.NotZero(exitCode);
                Assert.IsTrue(instance.ErrorData[0].EndsWith("error: token is invalid"));
            }
        }
        
        [Test]
        public async Task HiddenHeaders()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-headers");
            
            Assert.Zero(exitCode);
            Assert.IsTrue(instance.OutputData[0].EndsWith("testing-123"));
            Assert.AreEqual(1, instance.OutputData.Count);
        }
        
        [Test]
        public async Task HiddenTimestamps()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-timestamps");
            
            Assert.Zero(exitCode);
            Assert.AreEqual("testing-123", instance.OutputData[1]);
        }
        
        [Test]
        public async Task HiddenHeadersAndTimestamps()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "echo testing-123"});
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-headers --hide-timestamps");
            
            Assert.Zero(exitCode);
            Assert.AreEqual("testing-123", instance.OutputData[0]);
            Assert.AreEqual(1, instance.OutputData.Count);
        }
        
        [TestCase(10, 100, null, true)]
        [TestCase(10, 100, "txt", true)]
        [TestCase(1000, 10000, "txt", false)]
        [TestCase(0, 10, "txt", false)]
        [TestCase(1000, 10000, null, false)]
        [TestCase(0, 10, null, false)]
        [TestCase(10, 100, "json", false)]
        public async Task FileInput(long minSize, long maxSize, string acceptedExtension, bool success)
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script { Command = "cat $(test_file)", Files = new Dictionary<string, ScriptFileArgument>
            {
                {"test_file", new ScriptFileArgument
                {
                    MaxSize = maxSize,
                    MinSize = minSize,
                    AcceptedExtensions = string.IsNullOrEmpty(acceptedExtension) ? null : new [] { acceptedExtension }
                }}
            } });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-headers --hide-timestamps -f test_file=E2E/Files/testfile.txt");

            if (success)
            {
                Assert.Zero(exitCode);
                Assert.AreEqual(1, instance.OutputData.Count);
                Assert.AreEqual("the quick brown fox jumps over the lazy dog", instance.OutputData[0]);
            }
            else
            {
                Assert.NotZero(exitCode);
                Assert.AreEqual(2, instance.ErrorData.Count);
                Assert.IsTrue(instance.ErrorData[1].StartsWith("test_file failed:"));
            }
        }

        [Test]
        public async Task MissingArgument_NoDefaultValue()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script
            {
                Command = "echo $(test_var)",
                Variables = new Dictionary<string, ScriptArgumentDefinition>
                {
                    { "test_var", new ScriptArgumentDefinition() }
                }
            });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-headers --hide-timestamps");
            
            Assert.NotZero(exitCode);
            Assert.AreEqual(2, instance.ErrorData.Count);
            Assert.AreEqual("error: One or more validation errors occured", instance.ErrorData[0]);
            Assert.AreEqual("test_var failed: No value provided", instance.ErrorData[1]);
        }

        [Test]
        public async Task MissingArgument_DefaultValue()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script
            {
                Command = "echo $(test_var)",
                Variables = new Dictionary<string, ScriptArgumentDefinition>
                {
                    { "test_var", new ScriptArgumentDefinition { DefaultValue = "testing-123" } }
                }
            });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-headers --hide-timestamps");
            
            Assert.Zero(exitCode);
            Assert.AreEqual(1, instance.OutputData.Count);
            Assert.AreEqual("testing-123", instance.OutputData[0]);
        }

        [Test]
        public async Task LockedVariable_CannotProvide()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script
            {
                Command = "echo $(test_var)",
                Variables = new Dictionary<string, ScriptArgumentDefinition>
                {
                    { "test_var", new ScriptArgumentDefinition() }
                }
            });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token
            {
                AvailableScripts = new Dictionary<string, ScriptAccessDeclaration>
                {
                    {
                        "test", new ScriptAccessDeclaration
                        {
                            LockedVariables = new Dictionary<string, string>
                            {
                                {"test_var", "testing_321"}
                            }
                        }
                    }
                }
            });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-headers --hide-timestamps -v test_var=testing-123");
            
            Assert.NotZero(exitCode);
            Assert.AreEqual(2, instance.ErrorData.Count);
            Assert.AreEqual("error: One or more validation errors occured", instance.ErrorData[0]);
            Assert.AreEqual("test_var failed: Variable is locked and can not be provided", instance.ErrorData[1]);
        }

        [Test]
        public async Task LockedVariable_ValueIsUsed()
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script
            {
                Command = "echo $(test_var)",
                Variables = new Dictionary<string, ScriptArgumentDefinition>
                {
                    { "test_var", new ScriptArgumentDefinition() }
                }
            });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token
            {
                AvailableScripts = new Dictionary<string, ScriptAccessDeclaration>
                {
                    {
                        "test", new ScriptAccessDeclaration
                        {
                            LockedVariables = new Dictionary<string, string>
                            {
                                {"test_var", "testing_321"}
                            }
                        }
                    }
                }
            });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput("invoke test http://localhost:5000 -t test --hide-headers --hide-timestamps");
            
            Assert.Zero(exitCode);
            Assert.AreEqual(1, instance.OutputData.Count);
            Assert.AreEqual("testing_321", instance.OutputData[0]);
        }

        [TestCase("_mytestvar_", null, null, true)]
        [TestCase("_mytestvar_", "^_.*test.*_$", null, true)]
        [TestCase("_mytestvar", "^_.*test.*_$", null, false)]
        [TestCase("_mytestvar_", null, "^_.*test.*_$", true)]
        [TestCase("_mytestvar", null, "^_.*test.*_$", false)]
        [TestCase("_mytestvar_", "^_.*test.*_$", "^_.*test.*_$", true)]
        [TestCase("_mytestvar", "^_.*test.*_$", "^_.*test.*_$", false)]
        [TestCase("_mytestvar_", "^_.*test.*_$", "^_my", true)]
        [TestCase("_mytestvar", "^_.*test.*_$", "^my", false)]
        public async Task ContrainedVariables(string testValue, string scriptConstraint, string tokenConstraint, bool shouldSucceed)
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script
            {
                Command = "echo $(test_var)",
                Variables = new Dictionary<string, ScriptArgumentDefinition>
                {
                    { "test_var", new ScriptArgumentDefinition { Regex = scriptConstraint } }
                }
            });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token
            {
                AvailableScripts = !string.IsNullOrEmpty(tokenConstraint)
                    ? new Dictionary<string, ScriptAccessDeclaration>
                    {
                        {
                            "test", new ScriptAccessDeclaration
                            {
                                VariableContraints = new Dictionary<string, string>
                                {
                                    { "test_var", tokenConstraint }
                                }
                            }
                        }
                    }
                    : null
                
            });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput($"invoke test http://localhost:5000 -t test --hide-headers --hide-timestamps -v test_var={testValue}");
            
            if (shouldSucceed)
            {
                Assert.Zero(exitCode);
                Assert.AreEqual(1, instance.OutputData.Count);
                Assert.AreEqual(testValue, instance.OutputData[0]);
            }
            else
            {
                Assert.NotZero(exitCode);
                Assert.AreEqual(2, instance.ErrorData.Count);
                Assert.AreEqual("error: One or more validation errors occured", instance.ErrorData[0]);
                if (string.IsNullOrEmpty(scriptConstraint) && !string.IsNullOrEmpty(tokenConstraint))
                {
                    Assert.AreEqual($"test_var failed: Provided value does not pass profile constraint regex validation ({tokenConstraint})", instance.ErrorData[1]);
                }
                else if (!string.IsNullOrEmpty(scriptConstraint))
                {
                    Assert.AreEqual($"test_var failed: Provided value does not pass script regex validation ({scriptConstraint})", instance.ErrorData[1]);
                }
            }
        }

        
        [TestCase("12.1", ScriptArgumentType.Integer, false)]
        [TestCase("1200", ScriptArgumentType.Integer, true)]
        [TestCase("test1200", ScriptArgumentType.Integer, false)]
        [TestCase("1200test", ScriptArgumentType.Integer, false)]
        [TestCase("test", ScriptArgumentType.Integer, false)]
        
        [TestCase("12.1", ScriptArgumentType.Float, true)]
        [TestCase("test12.1", ScriptArgumentType.Float, false)]
        [TestCase("12.1test", ScriptArgumentType.Float, false)]
        [TestCase("1200", ScriptArgumentType.Float, false)]
        [TestCase("test", ScriptArgumentType.Float, false)]
        
        [TestCase("12.1", ScriptArgumentType.String, true)]
        [TestCase("1200", ScriptArgumentType.String, true)]
        [TestCase("test", ScriptArgumentType.String, true)]
        public async Task VariableValidation_InbuiltTypes(string variableValue, ScriptArgumentType scriptArgumentType, bool success)
        {
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Reset();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script
            {
                Command = "echo $(test_var)",
                Variables = new Dictionary<string, ScriptArgumentDefinition>
                {
                    { "test_var", new ScriptArgumentDefinition { Type = scriptArgumentType } }
                }
            });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput($"invoke test http://localhost:5000 --hide-timestamps -t test -v test_var={variableValue}");

            Assert.AreEqual(success, exitCode == 0);
            if (!success) Assert.IsTrue(instance.ErrorData[1].StartsWith("test_var failed: Provided value does not pass type validation"));
            else Assert.AreEqual(variableValue, instance.OutputData[1]);
        }
        
        [TestCase(true, true)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(false, false)]
        public async Task SecretsStaySecret(bool definedAsSecret, bool providedAsSecret)
        {
            const string secret = "myverysecretsecret";
            var scriptReaderMock = _host.Services.GetRequiredService<Mock<IScriptReader>>();
            scriptReaderMock.Reset();
            scriptReaderMock.Setup(s => s.Load("test")).ReturnsAsync(new Script
            {
                Command = "echo $(test_var)",
                ShowOutput = true,
                ShowScript = true,
                Variables = new Dictionary<string, ScriptArgumentDefinition>
                {
                    { "test_var", new ScriptArgumentDefinition { Secret = definedAsSecret } }
                }
            });
            var tokenReaderMock = _host.Services.GetRequiredService<Mock<ITokenReader>>();
            tokenReaderMock.Setup(s => s.ParseTokenFile("test", It.IsAny<CancellationToken>())).ReturnsAsync(new Token { AvailableScripts = new Dictionary<string, ScriptAccessDeclaration> { {"test", new ScriptAccessDeclaration()} } });
            
            var (exitCode, instance) = await E2ETestUtils.ClientOutput($"invoke test http://localhost:5000 --hide-headers --hide-timestamps -t test {(providedAsSecret ? "-s" : "-v")} test_var={secret}");

            var shouldBeSecret = definedAsSecret || providedAsSecret;
            Assert.Zero(exitCode);
            Assert.AreEqual(2, instance.OutputData.Count);
            Assert.AreEqual(0, instance.ErrorData.Count);
            
            if (shouldBeSecret)
            {
                Assert.False(instance.OutputData.Any(s => s.Contains(secret)));
                Assert.False(instance.ErrorData.Any(s => s.Contains(secret)));
                Assert.AreEqual("echo " + new string('*', secret.Length), instance.OutputData[0]);
                Assert.AreEqual(new string('*', secret.Length), instance.OutputData[1]);
            }
            else
            {
                Assert.AreEqual("echo " + secret, instance.OutputData[0]);
                Assert.AreEqual(secret, instance.OutputData[1]);
            }
        }
    }
}