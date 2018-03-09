angular.module("frontend").controller("ExperimentViewController", function ($scope, $http, $route, $routeParams, $location, Utils, Notification) {
    $scope.sortType = 'Id';
    $scope.sortReverse = false;
    $scope.searchFilter = "";
    $scope.doublecheck = false;

    $scope.getExperimentInstances = function (pageId) {
        pageId = pageId || 1;

        var experimentStatus = $scope.showJustErrors ? "Error" : "all";
        $http.get("experiments/" + $routeParams.id + "/" + pageId + "/" + experimentStatus).then(function (r) {
            $scope.experimentInstancesData = r.data;
        }, Utils.handleApiError);
    };

    $scope.getExperimentParameters = function () {
        $http.get("experiments/" + $routeParams.id + "/parameters").then(function (r) {
            $scope.experimentParameters = r.data;
        }, Utils.handleApiError);
    };

    var init = function () {
        $http.get("experiments/" + $routeParams.id).then(function (r) {
            $scope.experiment = r.data;
            /*
             * The data contains an empty parameter list for legacy reasons.
             * The scope.experimentParamters should be used instead.
             * We remove this list to avoid accessing the wrong paramter list.
             */
            delete $scope.experiment.Parameters;

            $scope.capabilities = $scope.experiment.RequiredCapabilities;
            $scope.setTimeoutValue = $scope.experiment.Timeout;
        }, Utils.handleApiError);

        $scope.getExperimentInstances(1);
        $scope.getExperimentParameters();
    }
    
    init();

    $scope.toggleSort = function (index) {
        $scope.sortReverse = !$scope.sortReverse;
        $scope.sortType = function (parameter) {
            return parameter.Configuration[$scope.experiment.Parameters[index].Name];
        }
    }

    $scope.setTimeout = function () {
        $http.post("experiments/" + $routeParams.id + "/timeout", { "Timeout": $scope.setTimeoutValue }).then(function (r) {
            Notification("Timeout changed.");
            $route.reload();
        }, Utils.handleApiError);
    };

    function setCapabiltiesAtServer(capabilities) {
        $http.post("experiments/" + $routeParams.id + "/requiredCapabilities", { "Capabilities": capabilities }).then(function (r) {
            Notification("Required capabilities changed.");
            $route.reload();
        }, Utils.handleApiError);
    }

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

        var tmp = $scope.capabilities.slice();
        tmp.push(capName);
        setCapabiltiesAtServer(tmp);
    };

    $scope.removeCapability = function (capName) {
        var tmp = $scope.capabilities.slice();
        var index = tmp.indexOf(capName);
        if (index > -1) {
            tmp.splice(index, 1);
            setCapabiltiesAtServer(tmp);
        }
    };

    $http.get("experiments/" + $routeParams.id + "/remainingTime").then(function (r) {
        $scope.remainingTime = r.data;
    }, Utils.handleApiError);

    $scope.dataLink = "experiments/" + $routeParams.id + "/data.json";
    $scope.dataLinkCsv = "experiments/" + $routeParams.id + "/data.csv";

    $scope.getStatusBgClass = function (statusId) {
        return ["bg-warning", "bg-info", "bg-success", "bg-danger", "bg-danger"][statusId];
    };

    $scope.getStatusCellBgClass = function (statusId) {
        return ["warning", "info", "success", "danger", "bg-danger"][statusId];
    };

    $scope.instanceHasWarnings = function (instance) {
        return instance.LogMessages.some(function (lm) {return lm.Type == 1});
    };

    $scope.exportNotebook = function (force) {
        $http.post("experiments/" + $routeParams.id + "/exportNotebook/" + (force || false)).then(function (r) {
            if (r.data.Message && confirm('A jupyter notebook file exists already. Do you want to override it?')) {
                $scope.exportNotebook(true);
            } else {
                Notification("Now opening notebook at " + r.data.ExportDir + ".");
                /* we assume that jupyter has the jupyter folder as root folder */
                window.open("http://" + $location.host() + ":8888/notebooks/" + r.data.FileName + "/notebook.ipynb");
            }
        }, Utils.handleApiError);
    };

    $scope.addParameterValue = function (parameter, value) {
        $http.post("experiments/" + $routeParams.id + "/addParameterValue", {"ParameterName": parameter.Name, "Value": value}).then(function (r) {
            Notification("Parameter value has been added.");
            $route.reload();
        }, Utils.handleApiError);
    };

    $scope.addSeeds = function () {
        maxSeedSoFar = 0;
        for (var k = 0; k < $scope.experimentParameters.length; k++) {
            if ($scope.experimentParameters[k].Name == "seed") {
                var seedPara = $scope.experimentParameters[k];

                for (var j = 0; j < seedPara.Values.length; j++) {
                    var valueStr = seedPara.Values[j];
                    var value = Number.parseInt(valueStr);
                    if (value > maxSeedSoFar) {
                        maxSeedSoFar = value;
                    }
                }
                break;
            }
        }

        var addSeedsSequentially = function (numberOfSeeds) {
            if (numberOfSeeds == 0) {
                $route.reload();
            } else {
                var nextSeed = maxSeedSoFar + numberOfSeeds;
                $http.post("experiments/" + $routeParams.id + "/addParameterValue", { "ParameterName": "seed", "Value": nextSeed }).then(
                    function (r) {
                        addSeedsSequentially(numberOfSeeds - 1);
                    },
                    Utils.handleApiError);
            }
        }

        addSeedsSequentially(Number.parseInt($scope.addNumberOfSeeds));
    };
    
    $scope.abortExperiment = function () {
        if (!$scope.doublecheck) {
            $scope.doublecheck = true;
            Notification("Do you really want to abort the experiment? Please Enter the experiment Id.");
        } else if ($scope.doublecheckSimId == $routeParams.id) {
            $http.post("experiments/" + $routeParams.id + "/abort").then(function (r) {
                Notification("The experiment has been aborted.");
                $route.reload();
            }, Utils.handleApiError);
        } else {
            Notification("Invalid simualtion Id.");
        }
    };

    $scope.resetExperiment = function () {
        if (!$scope.doublecheck) {
            $scope.doublecheck = true;
            Notification("Do you really want to reset the experiment? Please Enter the experiment Id.");
        } else if ($scope.doublecheckSimId == $routeParams.id) {
            $http.post("experiments/" + $routeParams.id + "/reset", { "Status": undefined }).then(function (r) {
                Notification(r.data.Count + " experiments have been reset.");
                $route.reload();
            }, Utils.handleApiError);
        } else {
            Notification("Invalid simualtion Id.");
        }
    };

    $scope.stoneClone = function () {
        $http.post("experiments/" + $routeParams.id + "/stoneClone").then(function (r) {
                Notification("Experiment has been Stone Cloned.");
                $location.path("/experiments/" + r.data.ExperimentId);
            }, Utils.handleApiError);
    };

    $scope.resetExperimentNoCheck = function (status) {
        $http.post("experiments/" + $routeParams.id + "/reset", { "Status": status }).then(function (r) {
            Notification(r.data.Count + " experiments have been reset.");
            $route.reload();
        }, Utils.handleApiError);
    };

    $scope.resetExperimentInstance = function (instanceId) {
        $http.post("experiments/" + $routeParams.id + "/instances/" + instanceId + "/reset").then(function (r) {
            Notification("The experiment instance has been reset.");
            $route.reload();
        }, Utils.handleApiError);
    };

    $scope.deleteExperiment = function () {
        if (!$scope.doublecheck) {
            $scope.doublecheck = true;
            Notification("Do you really want to delete the experiment? Please Enter the experiment Id.");
        } else if ($scope.doublecheckSimId == $routeParams.id) {
            $http.delete("experiments/" + $routeParams.id).then(function (r) {
                Notification("The experiment has been deleted.");
                $location.path("experiments");
            }, Utils.handleApiError);
        } else {
            Notification("Invalid simualtion Id.");
        }
    };
});
