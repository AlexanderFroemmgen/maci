angular.module("frontend").controller("ExperimentListController", function ($scope, $http, Utils) {
    $http.get("experiments").then(function (r) {
        $scope.experiments = r.data;
    }, Utils.handleApiError);

    $scope.getStatusCellBgClass = function (statusId) {
        return ["warning", "info", "success", "danger", "danger"][statusId];
    };
});