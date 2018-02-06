angular.module("frontend").controller("HostListController", function ($scope, $http, $location, Utils, Notification, $route) {

    $scope.reload = function () {
        $http.get("workerhosts/images").then(function (r) {
            $scope.images = r.data;
        }, Utils.handleApiError);

        $http.get("workerhosts/instances").then(function (r) {
            $scope.instances = r.data;
        }, Utils.handleApiError);

        $http.get("workerhosts/timeout").then(function (r) {
            $scope.maxIdleTimeSec = r.data.Timeout;
        }, Utils.handleApiError);
    }

    $scope.backend = $location.host() + ":" + $location.port();

    if ($location.host() === "localhost") {
        $scope.warning_backend_localhost = true;
    }

    $scope.reload();

    $scope.launch = function (hostId, imageId) {
        $http.post("workerhosts/instances", {
            HostId: hostId,
            ImageId: imageId
        }).then(function (r) {
            Notification("Instance was launched successfully. It may take several minutes until it's fully booted.");
            $scope.reload();
        }, Utils.handleApiError);
    };

    $scope.terminate = function (hostId, instanceId) {
        $http.delete("workerhosts/" + hostId + "/instances/" + instanceId)
            .then(function (r) {
                $scope.reload();
            }, Utils.handleApiError);
    };

    $scope.setScaling = function (imageId, value) {
        $http.post("workerhosts/images/" + imageId + "/active/" + value)
            .then(function (r) {
                $scope.reload();
            }, Utils.handleApiError);
    };

    $scope.setTimeout = function () {
        $http.post("workerhosts/timeout", { "Timeout": $scope.maxIdleTimeSec }).then(function (r) {
            Notification("Timeout changed.");
            $route.reload();
        }, Utils.handleApiError);
    };
});