angular.module("frontend").controller("GlobalEventLogController", function($scope, $http, Utils) {
    $http.get("event_log").then(function (r) {
        $scope.eventLog = r.data;
    }, Utils.handleApiError);
});