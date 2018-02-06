angular.module("frontend").controller("HelpConceptsController", function ($scope, $http, $location, State, Utils) {
    $scope.datalocation = "<pending>";

    $http.get("framework/datalocation").then(function (r) {
        $scope.datalocation = r.data.DataLocation;
    }, Utils.handleApiError);
});