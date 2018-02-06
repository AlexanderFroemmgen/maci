angular.module('frontend')
.directive('dragDropUploadTarget', function ($document, $http, Notification) {
    return {
        scope: {
            onUploadSuccessful: '&'
        },
        link: function (scope, element, attr) {
            element.on('dragover', function (e) {
                e.preventDefault();
                e.stopPropagation();
            });
            element.on('dragenter', function (e) {
                e.preventDefault();
                e.stopPropagation();
            });
            element.on('drop', function (e) {
                e.preventDefault();
                e.stopPropagation();
                if (e.dataTransfer) {
                    if (e.dataTransfer.files.length > 0) {
                        upload(e.dataTransfer.files);
                    }
                }
                return false;
            });
            var upload = function (files) {
                Notification("Uploading "+(files.length === 1?"file":""+files.length+" files")+"...");

                var data = new FormData();
                angular.forEach(files, function (value, i) {
                    data.append("files" + i, value);
                });

                $http({
                    method: 'POST',
                    url: attr.uploaduri,
                    data: data,
                    withCredentials: true,
                    headers: { 'Content-Type': undefined },
                    transformRequest: angular.identity
                }).success(function () {
                    Notification("Upload successful");
                    scope.onUploadSuccessful();
                }).error(function () {
                    Notification.error("Upload failed.");
                });
            };
        }
    };
});

