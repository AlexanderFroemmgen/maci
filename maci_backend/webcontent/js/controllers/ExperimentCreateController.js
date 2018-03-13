angular.module("frontend").controller("ExperimentCreateController",
    function ($scope, $http, $window, $location, $timeout, Notification, Utils, State) {
        var loadDataLocation = function () {
            $scope.datalocation = "<pending>";

            $http.get("framework/datalocation").then(function (r) {
                $scope.datalocation = r.data.DataLocation;
            }, Utils.handleApiError);
        };

        loadDataLocation();

        var reloadExperimentTemplateNames = function() {
            $http.get("experimentFiles").then(function (r) {
                $scope.namesOfStoredSims = r.data;
            }, Utils.handleApiError);
        };

        reloadExperimentTemplateNames();

        $scope.PARAM_TYPES = ["string", "int", "float"];
        $scope.PARAM_PURPOSES = ["CFG", "ENV"];

        var confDeclRegex = /^\s*###\s+([A-Za-z]+)\s+([A-Za-z]+)\s+([A-Za-z0-9_]+)\s+(?:\[([^\]]+)\]\s+)?"([^"]+)"\s*$/mg;
        //var confCapRegex = /^\s*###\s+REQUIRE\s+([A-Za-z0-9_]+)\s*$/mg;
        $scope.parseParameterDeclarations = function () {
            var scriptCode = $scope.editor.getValue();

            $scope.parameterDeclarations = [];
            confDeclRegex.lastIndex = 0;
            while ((match = confDeclRegex.exec(scriptCode)) != null) {
                var type = $scope.PARAM_TYPES.indexOf(match[2].toLowerCase());
                var purpose = $scope.PARAM_PURPOSES.indexOf(match[1].toUpperCase());
                var keywords = ["seed", "simId", "simInstanceId"];
                if (keywords.includes(match[3])) {
                    Notification.error("This parameter name is prohibited.\n" + match[0]);
                    continue;
                }
                if (type == -1 || purpose == -1) {
                    Notification.error("invalid parameter declaration\n" + match[0]);
                    continue;
                }
                $scope.parameterDeclarations.push({
                    "Name": match[3],
                    "Type": type,
                    "Purpose": purpose,
                    "Description": match[5],
                    "Unit": match[4]
                });
                if (!$scope.parameterValues[match[3]]) $scope.parameterValues[match[3]] = [];
            }
            /* $scope.capabilities = [];
            confCapRegex.lastIndex = 0;
            while ((match = confCapRegex.exec(scriptCode)) != null) {
                $scope.capabilities.push(match[1]);
            }*/
        };

        $scope.newParam = { 'Type': 1, 'Purpose': 0 };

        var sanitizeNewParam = function (param) { 
            if (!param.Name) {
                Notification.error("No parameter name specified.");
                return;
            }

            param.Name = param.Name.trim();
            param.Name = param.Name.replace(/[^a-zA-Z0-9-_]/g, "_");
            param.Unit = param.Unit ? param.Unit.trim() : "";

            if (param.Name == "") {
                Notification.error("No parameter name specified.");
                return;
            }

            if ($scope.parameterDeclarations && $scope.parameterDeclarations.some(function (p) { return p.Name === param.Name })) {
                Notification.error("Parameter name already specified.");
                return;
            }

            var keywords = ["seed", "simId", "simInstanceId"];
            if (keywords.includes(param.Name)) {
                Notification.error("This parameter name is prohibited.");
                return;
            }
            return param;
        };

        $scope.addParam = function () {
            var newParam = sanitizeNewParam($scope.newParam);

            if (!newParam) {
                return;
            }

            var decl = "### " +
                $scope.PARAM_PURPOSES[newParam.Purpose] +
                " " +
                $scope.PARAM_TYPES[newParam.Type] +
                " " +
                newParam.Name;

            if (newParam.Unit) {
                decl += " [" + newParam.Unit + "]";
            }

            decl += " \"write description here...\"";

            var scriptCode = $scope.editor.getValue();
            var lines = scriptCode.split(/\n/);
            for (var i = 0; i < lines.length; i++) {
                if (!lines[i].startsWith("###")) break;
            }
            lines[i] = decl + "\n" + lines[i];
            $scope.editor.setValue(lines.join("\n"));
            $scope.newParam.Name = "";
            $scope.parseParameterDeclarations();
        };

        /* Ctrl + S stores experiments,
           Ctrl + Return runs experiments */
        $scope.keyDownFunc = function($event) {
            var sKey = 83;
            var returnKey = 13;

            if ($event.ctrlKey && $event.keyCode === sKey) {
                $event.preventDefault();
                /* storeSim stores both the experiment and the current config */
                $scope.storeSim();
            } else if ($event.ctrlKey && $event.keyCode === returnKey) {
                $event.preventDefault();
                $scope.run();
            }
        };

        $scope.loadSim = function (selectedSimName) {
            $http.get("experimentFiles/" + selectedSimName).then(function (r) {
                if (r.data === "") {
                    Notification.error("Experiment study template " + selectedSimName + " could not be loaded."); 
                    return;
                }
                $scope.configurations = r.data.Configurations;
                $scope.editor.setValue(r.data.Script || "");
                $scope.editor_install.setValue(r.data.ScriptInstall || "");
                $scope.noInstallScript = r.data.ScriptInstall === "";
                State.experimentTemplateName = selectedSimName;
                $scope.selectedSimName = selectedSimName;
                $scope.storedSimName = selectedSimName;
                $scope.parseParameterDeclarations();

		        /* transform config names */
                $scope.namesOfStoredSimConfigs = [];
                for (var config in $scope.configurations) {
                    if (!$scope.configurations.hasOwnProperty(config)) {
                        continue;
                    }
                    $scope.namesOfStoredSimConfigs.push(config);
                }

                var defaultConfigName = "default";
                if (State.configName && $scope.configurations[State.configName]) {
                    $scope.selectedSimConfigName = State.configName;
                    $scope.loadSimConfig(State.configName);
                } else if ($scope.configurations[defaultConfigName]) {
                    $scope.selectedSimConfigName = defaultConfigName;
                    $scope.loadSimConfig(defaultConfigName);
                } else {
                    $scope.parameterDeclarations = [];
                    $scope.parameterValues = {};
                    $scope.capabilities = [];
                }
                Notification("Experiment " + selectedSimName + " was loaded.");
                $scope.loadFrameworkFileList();

                $http.get("experimentFiles/" + $scope.selectedSimName + "/history").then(function (r) {
                    $scope.simHistory = r.data.History;
                    $scope.repoRemoteUrl = r.data.RepoRemoteUrl;
                });
            }, Utils.handleApiError);
        };

        $scope.loadFrameworkFileList = function() {
            $http.get("framework/" + $scope.selectedSimName).then(function (r) {
                    $scope.files = r.data;
                },
                Utils.handleApiError);
        };

        $scope.deleteSim = function (selectedSimName) {
            $http.post("experimentFiles/" + selectedSimName + "/delete").then(function (r) {
                Notification("Experiment " + selectedSimName + " has been deleted.");
                reloadExperimentTemplateNames();
            }, Utils.handleApiError);
            reloadExperimentTemplateNames();
        };

        function getCurrentConfigurationJson() {
            return JSON.stringify({
                "paramValues": $scope.parameterValues, "capabilities": $scope.capabilities, "paramFilter": $scope.paramFilter, "repetitions": $scope.repetitions, "seeds":$scope.seeds });
        }

        $scope.storeSim = function () {
            if (!$scope.storedSimName || $scope.storedSimName.trim() === "") {
                Notification("Invalid empty experiment template name.");
                return;
            }

            var storedSimConfigName = $scope.storedSimConfigName;
            if (!storedSimConfigName || storedSimConfigName.trim() == "") {
                /* fail gently, use default name */
                storedSimConfigName = "default";
                $scope.storedSimConfigName = "default";
                //$scope.namesOfStoredSimConfigs.push("default");
            }

            var configurations = new Object();
            configurations[storedSimConfigName] = getCurrentConfigurationJson();

            $http.post("experimentFiles", {
                Name: $scope.storedSimName,
                Script: $scope.editor.getValue(),
                ScriptInstall: $scope.editor_install.getValue(),
                Configurations: configurations
            }).then(function (r) {
                reloadExperimentTemplateNames();
                Notification("Experiment was stored as " + $scope.storedSimName + " with configuration " + storedSimConfigName);
            }, function (r) {
                Utils.handleApiError(r);
            });
        };


        $scope.loadSimConfig = function (selectedSimConfigName) {
            var tmp = JSON.parse($scope.configurations[selectedSimConfigName]);
            State.configName = selectedSimConfigName;
            $scope.storedSimConfigName = selectedSimConfigName;
            $scope.capabilities = tmp.capabilities; 

            if (tmp.paramValues) { // Read new format
                $scope.parameterValues = tmp.paramValues;
            } else if (tmp.parameters) { // Convert from legacy format
                $scope.parameterValues = {};
                for(var i in tmp.parameters) {
                    if (tmp.parameters.hasOwnProperty(i)) {
                        var param = tmp.parameters[i];
                        $scope.parameterValues[param.Name || i] = param.Values;
                    }
                }
            } else { // No values at all
                $scope.parameterValues = {};
            }
            // Create empty values arrays for new declarations
            $scope.parameterDeclarations.forEach(function(param) {
                if (!$scope.parameterValues[param.Name]) $scope.parameterValues[param.Name] = [];
            });

            $scope.paramFilter = tmp.paramFilter;
            $scope.repetitions = tmp.repetitions;
            $scope.seeds = tmp.seeds;

        };

        $scope.deleteSimConfig = function (selectedSimConfigName) {
            $http.post("experimentFiles/" + $scope.selectedSimName + "/configs/" + selectedSimConfigName + "/delete").then(function (r) {
                Notification("Experiment config " + selectedSimConfigName + " has been deleted.");
                $scope.namesOfStoredSimConfigs.splice($scope.namesOfStoredSimConfigs.indexOf(selectedSimConfigName), 1);
            }, Utils.handleApiError);
            reloadExperimentTemplateNames();
        };

        $scope.storeSimConfig = function () {
            if (!$scope.storedSimName || $scope.storedSimName.trim() === "") {
                Notification("Invalid empty experiment template name.");
                return;
            }

            if (!$scope.storedSimConfigName || $scope.storedSimConfigName.trim() == "") {
                Notification("Invalid empty configuration name.");
                return;
            }

            $http.post("experimentFiles/" + $scope.storedSimName + "/configs", {
                Name: $scope.storedSimConfigName,
                Configuration: getCurrentConfigurationJson()
            }).then(function (r) {
                $scope.configurations[$scope.storedSimConfigName] = $scope.storedSimConfigName;
                $scope.namesOfStoredSimConfigs.push($scope.storedSimConfigName);
                $scope.selectedSimConfigName = $scope.storedSimConfigName;
                Notification("Experiment config was stored as " + storedSimConfigName + ".");
            }, function (r) {
                Utils.handleApiError(r);
            });
        };

        $scope.parameterValues = {
        };

        $scope.parameterDeclarations = [
        ];


        function getParametersForRuntime() {
            return $scope.parameterDeclarations.map(function(decl) {
                return Object.assign({ "Values": $scope.parameterValues[decl.Name] }, decl);
            })
        }

        $scope.addValue = function (paramName, value) {
            // Sanitize the value a bit...
            var parameter = $scope.parameterDeclarations.find(function (p) {return p.Name === paramName});
            if (parameter.Type == 0) {
                value = value.trim();
                value = value.replace(/[^a-zA-Z0-9-_\.]/g, "_");
            }
            else if (parameter.Type == 1) {
                value = parseInt(value, 10)
            }
            else if (parameter.Type == 2) {
                value = Number(value);
            }

            // Second condition is check for NaN
            if (value === "" || value !== value) {
                Notification.error("Value is invalid.");
                return;
            }

            if ($scope.parameterValues[paramName].indexOf(value) > -1) {
                Notification.error("This value already exists.");
                return;
            }

            $scope.parameterValues[paramName].push(value);
        };

        $scope.removeValue = function (paramName, value) {
            var index = $scope.parameterValues[paramName].indexOf(value);
            if (index > -1) {
                $scope.parameterValues[paramName].splice(index, 1);
            }
        };

        $scope.calculateNumberOfInstances = function () {
            var count = 1;
            for (var param in $scope.parameterValues) {
                if (!$scope.parameterValues.hasOwnProperty(param)) {
                    continue;
                }

                count *= $scope.parameterValues[param].length;
            }

            count *= $scope.repetitions;
            count *= $scope.seeds;

            return count;
        };

        // Capabilities required by this experiment. 
        $scope.capabilities = ['mininet'];

        $scope.addCapability = function (capName) {
            capName = capName.trim();
            capName = capName.replace(/[^a-zA-Z0-9-_]/g, "_");
            if (capName == "") {
                return;
            } 

            if ($scope.capabilities.indexOf(capName) > -1) {
                Notification.error("A capability with this name already exists.");
                return;
            } 
            $scope.capabilities.push(capName);
        };

        $scope.removeCapability = function (capName) {
            var index = $scope.capabilities.indexOf(capName);
            if (index > -1) {
                $scope.capabilities.splice(index, 1);
            } 
        }; 


        $scope.testStatusRefresher = function () {
            $http.get("experiments/" + $scope.testStatus.simId + "/instances/" + $scope.testStatus.simInstanceId).then(function (r) {
                console.log(r.data);
                $scope.testStatus.lastResult = r.data;
                $scope.testStatus.timerS += 1;
                
                if ($scope.testStatus.lastResult.Status == 0 || $scope.testStatus.lastResult.Status == 2) { // pending or running?
                    $timeout($scope.testStatusRefresher, 1000);
                } else {
                    $scope.testStatus.running = false;
                }
            }, Utils.handleApiError);
        }

        $scope.run = function (testRun) {
            if (testRun && $scope.testStatus && $scope.testStatus.running) {
                Notification('There is still a running test...');
                return;
            }

            var fileName = State.experimentTemplateName;
            if (!fileName || fileName.trim() == "") {
                fileName = "Anonymous";
            }

            testRun = testRun || false;

            var payload = {
                Script: $scope.editor.getValue(),
                ScriptInstall: $scope.noInstallScript ? "" : $scope.editor_install.getValue(),
                Parameters: getParametersForRuntime(),
                RequiredCapabilities: $scope.capabilities,
                Language: "Python",
                PermutationFilter: $scope.paramFilter,
                Repetitions: $scope.repetitions,
                Seeds: $scope.seeds,
                RunName: testRun ? ("Test Run " + $scope.selectedSimConfigName) : ($scope.runName || $scope.selectedSimConfigName),
                FileName: fileName,
                TestRun: testRun
            };

            $scope.update_maci_msg = "";
            $scope.update_maci_error = "";

            $scope.getStatusCellBgClass = function (statusId) {
                return ["warning", "info", "success", "danger", "danger"][statusId];
            };

            $http.post("experiments", payload, {timeout: 6000}).then(function (r) {
                console.log(r.data);
              
               if (r.data.Failed) {
                   $scope.update_maci_msg = r.data.Message;
                   $scope.update_maci_error = r.data.ErrorMessage;
               } else {
                   if (testRun) {
                       $scope.testStatus = {
                           simId : r.data.ExperimentId,                      
                           simInstanceId: r.data.ExperimentInstanceId,
                           running: true,
                           timerS: 0
                       };
                       $timeout($scope.testStatusRefresher, 1000);
                   } else {
                       Notification('Experiment was created!');
                       $location.path("/experiments/" + r.data.ExperimentId);
                   }
               }
            }, function (r) {
                Utils.handleApiError(r);
            }); 
        };

        require(['vs/editor/editor.main'], function () {
            var editor = monaco.editor.create(document.getElementById('code-editor'), {
                language: 'python',
                scrollBeyondLastLine: false
            });

            $scope.editor = editor;
            $scope.update_maci_msg = "";
            $scope.update_maci_error = "";

            $scope.editor_install = monaco.editor.create(document.getElementById('code-editor-install'), {
                language: 'python',
                scrollBeyondLastLine: false
            });

            /* coming back per navigation */
            if (State.experimentTemplateName) {
                $scope.loadSim(State.experimentTemplateName);
                $scope.selectedSimName = State.experimentTemplateName;
            } else {
                $scope.loadSim("SimplePythonExample");
            }
        });
    });
