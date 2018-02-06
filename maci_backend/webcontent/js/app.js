require.config({ paths: { 'vs': 'lib/monaco-editor/min/vs' } });
angular.module("frontend", ["ngRoute", "ui-notification"])
    .directive('requiredCapabilities', function() {
        return {
            restrict: 'E',
            scope: false,
            templateUrl: 'views/directives/required-capabilities.html'
        }
    });
