const { scriptParameters } = require("./script_parameters");

describe("scriptParameters", () => {
    describe("when the script is empty", () => {
        let parameters;
        beforeEach(() => parameters = scriptParameters(""));

        it("has undefined parameters", () => {
            expect(parameters).toBeUndefined();
        });
    });

    describe("when the script exports only a function", () => {
        let parameters;
        beforeEach(() => parameters = scriptParameters("module.exports = () => {}"));

        it("has undefined parameters", () => {
            expect(parameters).toBeUndefined();
        });
    });

    describe("when the script is invalid", () => {
        let parameters;
        beforeEach(() => {
            jest.spyOn(global.console, 'error').mockImplementation();
            parameters = scriptParameters("not.valid.js")
        });

        afterEach(() => {
            global.console.error.mockRestore();
        });

        it("has undefined parameters", () => {
            expect(parameters).toBeUndefined();
        });

        it("logs the error", () => {
            expect(console.error).toBeCalled()
        });
    });

    describe("when the script loads dependencies", () => {
        let parameters;
        beforeEach(() => {
            jest.spyOn(global.console, 'error').mockImplementation();
            parameters = scriptParameters("require('tmp')")
        });

        afterEach(() => {
            global.console.error.mockRestore();
        });

        it("does not log errors", () => {
            expect(console.error).not.toBeCalled()
        });
    });

    describe("when the script exports parameters", () => {
        let parameters;
        beforeEach(() => parameters = scriptParameters("module.exports.params = {}"));

        it("return those params", () => {
            expect(parameters).toMatchObject({});
        });
    });
});
