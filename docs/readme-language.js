(function () {
    "use strict";

    function resolveLanguage(value) {
        var language = (value || "").toLowerCase().split(/[-_]/)[0];
        return language === "zh" || language === "ja" ? language : "en";
    }

    var requested = new URLSearchParams(window.location.search).get("lang");
    var language = resolveLanguage(requested || navigator.language);
    document.documentElement.lang = language === "zh" ? "zh-CN" : language;
    document.querySelectorAll("[data-language]").forEach(function (article) {
        article.hidden = article.dataset.language !== language;
    });
    document.querySelectorAll("[data-language-link]").forEach(function (link) {
        if (link.dataset.languageLink === language) {
            link.setAttribute("aria-current", "page");
        } else {
            link.removeAttribute("aria-current");
        }
    });
}());
