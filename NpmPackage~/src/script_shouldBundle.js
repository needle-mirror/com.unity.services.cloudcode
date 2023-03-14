#!/usr/bin/env node
const fs = require("fs");
const { infiniteProxy, handleError } = require("./proxy_env");

function withPatchedEnv(fn) {
    const tmpModule = module;
    const tmpRequire = require;
    const tmpConsole = console;

    try {
        module = {};
        module.exports = exports = {};
        require = infiniteProxy();
        console = infiniteProxy();

        fn();
    } catch (e) {
        handleError(e);
    } finally {
        module = tmpModule;
        require = tmpRequire;
        console = tmpConsole;
    }
}

function shouldBundle(source) {
    let bundling;
    withPatchedEnv(() => {
        eval(source);
        bundling = module?.exports?.bundling;
    });
    return bundling;
}
exports.shouldBundle = shouldBundle;

if (require.main === module) {
    const args = process.argv.slice(2);

    if(args[0] && fs.existsSync(args[0])){
        const source = fs.readFileSync(args[0])?.toString();
        const bundling = shouldBundle(source);
        const serialized = JSON.stringify(bundling);
        if(serialized){
            console.log(serialized);
        }
    }
}
