import { describe, expect, it } from 'vitest';
import { buildLoginFormRules } from '../loginValidation';

const t = (key: string) => key;

async function runValidator(rules: ReturnType<typeof buildLoginFormRules>['loginIdentifier'], value: string) {
    const patternRule = rules[3];
    if (!patternRule || typeof patternRule !== 'object' || !('validator' in patternRule)) {
        throw new Error('Expected pattern validator rule at index 3');
    }
    const validator = patternRule.validator;
    if (typeof validator !== 'function') {
        throw new Error('Expected validator function');
    }
    await validator({}, value);
}

describe('buildLoginFormRules', () => {
    it('builds four login identifier rules and two password rules', () => {
        const rules = buildLoginFormRules(t);
        expect(rules.loginIdentifier).toHaveLength(4);
        expect(rules.password).toHaveLength(2);
        expect(rules.loginIdentifier[0]).toMatchObject({
            required: true,
            message: 'common.auth.validation.loginIdentifierRequired',
        });
        expect(rules.password[1]).toMatchObject({
            max: 128,
            message: 'common.auth.validation.passwordMax',
        });
    });

    it('accepts valid email login identifiers', async () => {
        const rules = buildLoginFormRules(t);
        await expect(runValidator(rules.loginIdentifier, 'manager@firma.at')).resolves.toBeUndefined();
    });

    it('accepts valid username login identifiers', async () => {
        const rules = buildLoginFormRules(t);
        await expect(runValidator(rules.loginIdentifier, 'manager1')).resolves.toBeUndefined();
    });

    it('rejects username with invalid characters', async () => {
        const rules = buildLoginFormRules(t);
        await expect(runValidator(rules.loginIdentifier, 'bad name')).rejects.toThrow(
            'common.auth.validation.loginIdentifierPattern',
        );
    });
});
