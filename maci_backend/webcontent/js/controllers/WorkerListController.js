angular.module("frontend").controller("WorkerListController", function($scope, $http, Utils) {
    $http.get("workers").then(function(r) {
        $scope.workers = r.data;
    }, Utils.handleApiError);

    $scope.olderThanXMinutes = function (date, minutes) {
        return new Date() - new Date(date) > minutes * 60 * 1000;
    };
});