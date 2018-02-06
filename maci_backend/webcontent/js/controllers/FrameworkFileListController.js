angular.module("frontend").controller("FrameworkFileListController", function ($scope, $http, Utils, Notification) {
    $http.get("framework").then(function (r) {
        $scope.files = r.data;
    }, Utils.handleApiError);

    $scope.datalocation = "<pending>";

    $http.get("framework/datalocation").then(function (r) {
        $scope.datalocation = r.data.DataLocation;
    }, Utils.handleApiError);
});