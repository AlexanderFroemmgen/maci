angular.module("frontend").config(
    function ($routeProvider) {
        $routeProvider
            .when("/", {
                templateUrl: "/views/index.html"
            })
            .when("/experiments", {
                templateUrl: "/views/experiment/experiment_list.html"
            })
            .when("/experiment/create", {
                templateUrl: "/views/experiment/experiment_create.html"
            })
            .when("/experiments/:id", {
                templateUrl: "/views/experiment/experiment_view.html"
            })
            .when("/experiments/:simid/instances/:instanceid", {
                templateUrl: "/views/experiment/instance_view.html"
            })
            .when("/workers", {
                templateUrl: "/views/worker/worker_list.html"
            })
            .when("/workerhosts", {
                templateUrl: "/views/workerhosts/host_list.html"
            })
            .when("/framework", {
                templateUrl: "/views/framework/file_list.html"
            })
            .when("/export", {
                templateUrl: "/views/export/export.html"
            })
            .when("/help", {
                templateUrl: "/views/index.html"
            })
            .when("/help_concepts", {
                templateUrl: "/views/help/help_concepts.html"
            })
            .when("/help_language", {
                templateUrl: "/views/help/help_language.html"
            })
            .when("/help_api", {
                templateUrl: "/views/help/help_api.html"
            })
            .when("/help_analysis", {
                templateUrl: "/views/help/help_analysis.html"
            })
            .when("/help_git", {
                templateUrl: "/views/help/help_git.html"
            })
            .when("/help_faq", {
                templateUrl: "/views/help/help_faq.html"
            })
            .when("/global_event_log", {
                templateUrl: "/views/global_event_log/global_event_log.html"
            })
            .otherwise({ redirectTo: "/" });
    });