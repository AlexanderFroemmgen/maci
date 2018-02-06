angular.module("frontend").factory("Utils",
    function (Notification) {
        return {
            handleApiError: function (httpResponse) {
                console.log(httpResponse);
                Notification.error("<strong>" +
                    httpResponse.status +
                    " - " +
                    httpResponse.statusText +
                    "</strong><br>" +
                    JSON.stringify(httpResponse.data));

                if (httpResponse.status == -1) {
                    Notification.error("Server is not responding.");
                }
            }
        };
    });