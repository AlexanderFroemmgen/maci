angular.module("frontend").filter("formatStatus", function () {
    return function (status) {
        return ["Pending", "Finished", "Running", "Error", "Aborted"][status];
    };
});