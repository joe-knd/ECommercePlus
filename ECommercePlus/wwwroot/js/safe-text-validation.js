(function ($) {
    if (!$ || !$.validator || !$.validator.unobtrusive) {
        return;
    }

    var controlChars = /[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]/;

    function findProblem(value, params) {
        if (!value) {
            return null;
        }
        if (controlChars.test(value)) {
            return params.controlmsg;
        }
        if (params.markupRegex.test(value)) {
            return params.markupmsg;
        }
        if (params.sqlRegex.test(value)) {
            return params.sqlmsg;
        }
        return null;
    }

    $.validator.addMethod("safetext", function (value, element, params) {
        return this.optional(element) || findProblem(value, params) === null;
    });

    $.validator.unobtrusive.adapters.add("safetext", ["markup", "sql", "markupmsg", "sqlmsg", "controlmsg"], function (options) {
        var params = {
            markupRegex: new RegExp(options.params.markup, "i"),
            sqlRegex: new RegExp(options.params.sql, "i"),
            markupmsg: options.params.markupmsg,
            sqlmsg: options.params.sqlmsg,
            controlmsg: options.params.controlmsg
        };
        options.rules.safetext = params;
        options.messages.safetext = function (rule, element) {
            return findProblem($(element).val(), params) || options.message;
        };
    });
})(window.jQuery);
