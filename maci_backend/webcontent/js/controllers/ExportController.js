angular.module("frontend").controller("ExportController", function ($scope, $http, $location, Utils, Notification) {
    $http.get("experiments").then(function (r) {
        var tmp = r.data;
        for (var i = 0; i < tmp.length; i++) {
            tmp[i]["isChecked"] = false;
        }
        $scope.experiments = tmp;
    }, Utils.handleApiError);

    $scope.getStatusCellBgClass = function (statusId) {
        return ["warning", "info", "success", "danger", "danger"][statusId];
    };

    $scope.export = function () {
        var checked = new Array();
        for (var i = 0; i < $scope.experiments.length; i++) {
            if ($scope.experiments[i]["isChecked"]) {
                checked.push($scope.experiments[i].Id);
            }
        }
        
        $http.post("science/export", {
            Name: $scope.exportName,
            Description: $scope.exportDescription,
            Experiments: checked
        }).then(function (r) {
            Notification('Export data generated... Download starts in a few seconds.');
            window.open("science/" + r.data.Name + "/export.zip");
        }, function (r) {
            Utils.handleApiError(r)
        });
    };
});