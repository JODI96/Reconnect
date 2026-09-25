using System;
using NUnit.Framework;
using Reconnect.Client.UI.Screens;

namespace Reconnect.Client.Tests
{
    public sealed class RegisterRulesTests
    {
        private static readonly DateTime Today = new(2026, 9, 25);

        [TestCase("25.09.2008", true)]    // 18th birthday today
        [TestCase("26.09.2008", false)]   // one day short
        [TestCase("1.1.1990", true)]
        public void Age_check_matches_backend_rule(string birth, bool expectedAdult)
        {
            Assert.IsTrue(RegisterScreen.TryParseBirthDate(birth, out var date));
            Assert.AreEqual(expectedAdult, RegisterScreen.IsAdult(date, Today));
        }

        [TestCase("2008-09-25")]
        [TestCase("31.02.2000")]
        [TestCase("")]
        public void Invalid_birth_date_is_rejected(string text)
        {
            Assert.IsFalse(RegisterScreen.TryParseBirthDate(text, out _));
        }
    }
}
