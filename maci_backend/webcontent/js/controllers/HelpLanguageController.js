angular.module("frontend").controller("HelpLanguageController", function ($scope, $http, $location, State) {
    $scope.showExample = function (exampleName) {
        State.experimentTemplateName = exampleName;
        State.configName = "default";
        $location.path("/experiment/create");
    };
});