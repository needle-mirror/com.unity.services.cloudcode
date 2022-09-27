#!/usr/bin/env node
const fs = require("fs");

const infiniteProxy = () => {
    return new Proxy(function() {}, {
        get: () => infiniteProxy(),
        apply: () => infiniteProxy(),
    });
};

function withPatchedEnv(fn){
    const tmpModule = module;
    const tmpRequire = require;
    const tmpConsole = console;

    try{
        module = {};
        module.exports = exports = {};
        require = infiniteProxy();
        console = infiniteProxy();

        fn();
    } catch (e) {
        tmpConsole.error(e);
    } 
    finally {
        module = tmpModule;
        require = tmpRequire;
        console = tmpConsole;
    }
}

function scriptParameters(source){
    let parameters;
    withPatchedEnv(() => {
        eval(source);
        parameters = module?.exports?.params;
    });
    return parameters;
}
exports.scriptParameters = scriptParameters;

if (require.main === module) {
    const args = process.argv.slice(2);

    if(args[0] && fs.existsSync(args[0])){
        const source = fs.readFileSync(args[0])?.toString();
        const serialized = JSON.stringify(scriptParameters(source));
        if(serialized){
            console.log(serialized);
        }
    }
}
