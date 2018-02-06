/// <binding BeforeBuild='clean, default' />
"use strict";

var _ = require('lodash'),
    gulp = require('gulp'),
    uglify = require('gulp-uglify'),
    cssmin = require('gulp-cssmin'),
    rename = require('gulp-rename');
var del = require('del');
var htmlmin = require('gulp-htmlmin');

var js = [
    './node_modules/bootstrap/dist/js/bootstrap.js',
    './node_modules/jquery/dist/jquery.js',
    './node_modules/angular/angular.js',
    './node_modules/angular-route/angular-route.js',
    './node_modules/angular-ui-notification/dist/angular-ui-notification.js'
];

var css = [
    './node_modules/bootstrap/dist/css/bootstrap.css',
    './node_modules/angular-ui-notification/dist/angular-ui-notification.css'
];

gulp.task('copy-min-npm-js', function () {
    return gulp.src(js)
        //.pipe(uglify())
        .pipe(rename({ extname: '.min.js' }))
        .pipe(gulp.dest('./wwwroot/lib/js'))
});
gulp.task('copy-min-monaco', ['copy-min-npm-js'], function () {
    return gulp.src('./node_modules/monaco-editor/min/vs/**/*')
           .pipe(gulp.dest('./wwwroot/lib/monaco-editor/min/vs/'));
});
gulp.task('copy-min-npm-css', function () {
    return gulp.src(css)
        .pipe(cssmin())
        .pipe(gulp.dest('./wwwroot/lib/css'))
});

gulp.task('copy-webcontent-js', ['copy-min-npm-js', 'copy-min-monaco'], function () {
    return gulp.src("./webcontent/js/**/*")
         //   .pipe(uglify())
            .pipe(gulp.dest('./wwwroot/js'))
});
gulp.task('copy-webcontent-img', function () {
    return gulp.src("./webcontent/img/*.png")
            .pipe(gulp.dest('./wwwroot/img'))
});
gulp.task('copy-webcontent-css', ['copy-min-npm-css'], function () {
    return gulp.src("./webcontent/css/**/*")
            .pipe(cssmin())
            .pipe(gulp.dest('./wwwroot/css'))
});
gulp.task('copy-webcontent-views', function () {
    return gulp.src("./webcontent/views/**/*")
            .pipe(cssmin())
            .pipe(gulp.dest('./wwwroot/views'))
});
gulp.task('copy-webcontent-html', function () {
    return gulp.src("./webcontent/*.html")
            .pipe(cssmin()).pipe(htmlmin({collapseWhitespace: true}))
            .pipe(gulp.dest('./wwwroot/'))
});
gulp.task('copy-webcontent-templates', function () {
    return gulp.src("./webcontent/templates/**/*")
            .pipe(gulp.dest('./wwwroot/templates'))
});

gulp.task('clean', function () {
    return del([
      './wwwroot/**/*'
    ]);
});

gulp.task('default', ['copy-min-npm-js', 'copy-min-monaco', 'copy-min-npm-css', 'copy-webcontent-js', 'copy-webcontent-img', 'copy-webcontent-css', 'copy-webcontent-views', 'copy-webcontent-html', 'copy-webcontent-templates']);

