const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.resolve(__dirname, "..");
const script = fs.readFileSync(path.join(root, "docs/readme-language.js"), "utf8");
const html = fs.readFileSync(path.resolve(root, process.argv[2] || "artifacts/publish/README.html"), "utf8");
assert.ok(html.includes(script), "Generated help must embed the current language selector");
assert.ok(!html.includes("{{"), "Generated help must have no unresolved template markers");
assert.ok(!/<img[^>]+src="https?:/i.test(html), "Offline help must not load remote images");

for (const [preferred, query, expected] of [
    ["en-GB", "", "en"],
    ["ja-JP", "", "ja"],
    ["zh-TW", "", "zh"],
    ["fr-FR", "", "en"],
    ["ja-JP", "?lang=zh", "zh"],
    ["zh-CN", "?lang=en", "en"],
    ["en-US", "?lang=ja", "ja"],
    ["ja-JP", "?lang=unsupported", "en"]
]) {
    const articles = ["en", "zh", "ja"].map(language => ({ dataset: { language }, hidden: false }));
    const links = ["en", "zh", "ja"].map(languageLink => ({
        dataset: { languageLink },
        setAttribute(name, value) { this[name] = value; },
        removeAttribute(name) { delete this[name]; }
    }));
    const document = {
        documentElement: {},
        querySelectorAll(selector) { return selector === "[data-language]" ? articles : links; }
    };
    vm.runInNewContext(script, {
        document, navigator: { language: preferred },
        window: { location: { search: query } }, URLSearchParams
    });
    assert.deepEqual(articles.filter(article => !article.hidden).map(article => article.dataset.language), [expected]);
    assert.equal(document.documentElement.lang, expected === "zh" ? "zh-CN" : expected);
    assert.deepEqual(links.filter(link => link["aria-current"] === "page").map(link => link.dataset.languageLink), [expected]);
}

for (const file of ["README.md", "README.zh-CN.md", "README.ja.md"]) {
    const markdown = fs.readFileSync(path.join(root, file), "utf8");
    for (const match of markdown.matchAll(/\]\(([^)]+)\)/g)) {
        const target = match[1];
        if (!/^https?:/.test(target)) assert.ok(fs.existsSync(path.join(root, target)), file + ": missing " + target);
    }
}
console.log("README language selection, GUI language override, fallback and offline links: PASS");
