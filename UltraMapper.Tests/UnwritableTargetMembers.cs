﻿using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UltraMapper.Tests
{
    /// <summary>
    /// Test private fields and properties; 
    /// getter only properties
    /// </summary>
    [TestClass]
    public class NonPublicMembers
    {
        private class UnreadableMembers
        {
            private int field = 11;
            private int Property1 { get; set; } = 13;
            public int Property2 { private get; set; } = 17;
            private int Property3 { get; } = 19;
        }

        private class UnwritableMembers
        {
            private int field;
            private int Property1 { get; set; }
            public int Property2 { private get; set; }
            private int Property3 { get; }
            public int Property4 { get; }
        }

        [TestMethod]
        public void ReadNonPublicWriteNonPublic()
        {
            var source = new UnreadableMembers();
            var target = new UnwritableMembers();

            var mapper = new Mapper( config =>
            {
                //config.ConventionResolvers.SourceMemberProvider.IgnoreFields = false;
                //config.ConventionResolver.SourceMemberProvider.IgnoreNonPublicMembers = false;
                                                                           
                //config.ConventionResolver.TargetMemberProvider.IgnoreFields = false;
                //config.ConventionResolver.TargetMemberProvider.IgnoreNonPublicMembers = false;
            } );

            mapper.Map( source, target );

            var isResultOk = mapper.VerifyMapperResult( source, target );
            Assert.IsTrue( isResultOk );
        }
    }
}
