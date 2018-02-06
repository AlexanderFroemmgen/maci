angular.module("frontend").controller("ExperimentInstanceViewController", function($scope, $http, $routeParams, Utils) {
    var uri = "experiments/" + $routeParams.simid + "/instances/" + $routeParams.instanceid;
    $scope.sid = $routeParams.simid;
    $scope.instanceid = $routeParams.instanceid;

    $scope.recordsFilter = '';

    $http.get(uri).then(function(r) {
        $scope.instance = r.data;
    }, Utils.handleApiError);

    $http.get(uri + "/records").then(function(r) {
        $scope.records = r.data;
    }, Utils.handleApiError);

    $scope.copySSHConnectionString = function () {
        var origSelectionStart, origSelectionEnd;
        var target = document.getElementById("copyTarget");
        var currentFocus = document.activeElement;
        target.focus();
        target.setSelectionRange(0, target.value.length);

        // copy the selection
        var succeed;
        try {
            succeed = document.execCommand("copy");
        } catch (e) {
            succeed = false;
        }
        // restore original focus
        if (currentFocus && typeof currentFocus.focus === "function") {
            currentFocus.focus();
        }
    };
});
