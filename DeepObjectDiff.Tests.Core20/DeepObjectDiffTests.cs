using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace DeepObjectDiff.Tests.Core20
{
    public class DeepObjectDiffTests
    {
        [Theory]
        [InlineData("test", "test", true)]
        [InlineData("zażółć gęślą jaźń", "zażółć gęślą jaźń", true)]
        [InlineData("łaska", "laska", false)]
        [InlineData("", "", true)]
        [InlineData((string)null, (string)null, true)]
        [InlineData("this", "that", false)]
        [InlineData("this", "thIs", false)]
        public void Compare_ComparesStringsCorrectly_DefaultOptions(string first, string second, bool expectedEquals)
        {
            ObjectComparer.Compare(first, second, out _).Should().Be(expectedEquals, "result was manually determined");
        }

        [Theory]
        [InlineData("teSt", "test", true)]
        [InlineData("zażółć gęślą jaźń", "zAżółć gęślą jaźń", true)]
        [InlineData("łaska", "lasKa", false)]
        [InlineData("", "", true)]
        [InlineData((string)null, (string)null, true)]
        [InlineData("this", "tHat", false)]
        [InlineData("this", "thIs", true)]
        public void Compare_ComparesStringsCorrectly_CaseInsensitive(string first, string second, bool expectedEquals)
        {
            var options = new CompareOptions {DefaultStringComparison = StringComparison.OrdinalIgnoreCase};
            ObjectComparer.Compare(first, second, out _, options).Should().Be(expectedEquals, "result was manually determined");
        }

        [Theory]
        [InlineData(1,1,true)]
        [InlineData(1,-1,false)]
        [InlineData(-1,1,false)]
        [InlineData(0,0,true)]
        public void Compare_ComparesIntsCorrectly_DefaultOptions(int first, int second, bool expectedResult)
        {
            ObjectComparer.Compare(first, second, out _).Should().Be(expectedResult, "result was manually determined");
        }

        private class SimpleModel
        {
            public int Id { get; set; }
            public string Value { get; set; }
            public DateTime Timestamp { get; set; }
        }

        [Fact]
        public void Compare_ComparesPOCO_DefaultOptions_SameObject()
        {
            var model = new SimpleModel();
            ObjectComparer.Compare(model, model, out _).Should().BeTrue("it's the same object");
        }

        [Fact]
        public void Compare_ComparesPOCO_DefaultOptions_SameContent()
        {
            const int id = 123;
            const string value = "fnord";
            var date = DateTime.Now;

            var model1 = new SimpleModel
            {
                Id = id,
                Timestamp = date,
                Value = value
            };
            var model2 = new SimpleModel
            {
                Id = id,
                Timestamp = date,
                Value = value
            };
            ReferenceEquals(model1, model2).Should().BeFalse("They are not the same objects");
            ObjectComparer.Compare(model1, model2, out _).Should().BeTrue("they contain the same data");
        }

        [Fact]
        public void Compare_ComparesPOCO_DefaultOptions_DifferentContent()
        {
            const int id = 123;
            const string value = "fnord";
            var date = DateTime.Now;

            var model1 = new SimpleModel
            {
                Id = id,
                Timestamp = date,
                Value = value
            };
            var model2 = new SimpleModel
            {
                Id = id+1,
                Timestamp = date,
                Value = value
            };
            ReferenceEquals(model1, model2).Should().BeFalse("They are not the same objects");
            ObjectComparer.Compare(model1, model2, out _).Should().BeFalse("they do not contain the same data");
        }

        private class ComplexModel
        {
            public IList<int> NumberSequence { get; set; }
            public Uri Link { get; set; }
            public ISet<string> Codes { get; set; }
            public SimpleModel[] Simples { get; set; }
            public SimpleModel Simple { get; set; }
            public ComplexModel Nested { get; set; }
        }

        [Fact]
        public void Compare_ComparesComplexModels_DefaultOptions_SameObject()
        {
            const int id = 123;
            const string value = "fnord";
            var date = DateTime.Now;

            const string link = @"http://example.com/subpage/?param=1&other=test";

            var options = new CompareOptions().Use(EqualityComparer<Uri>.Default);

            var complex = new ComplexModel
            {
                Codes = new HashSet<string>(new[] { "A", "b", "cee" }),
                Link = new Uri(link),
                NumberSequence = new List<int> { 1, 2, 3 },
                Simple = new SimpleModel
                {
                    Id = id,
                    Timestamp = date,
                    Value = value
                },
                Simples = new[]
                {
                    new SimpleModel
                    {
                        Id = id+1,
                        Timestamp = date,
                        Value = value
                    },
                    new SimpleModel
                    {
                        Id = id+2,
                        Timestamp = date,
                        Value = value
                    }
                },
                Nested = new ComplexModel
                {
                    Codes = new HashSet<string>(new[] { "test1", "test2" })
                }
            };
            ReferenceEquals(complex, complex).Should().BeTrue("they are the same objects");
            ObjectComparer.Compare(complex, complex, out _, options).Should().BeTrue("they are the same object");
        }

        [Fact]
        public void Compare_ComparesComplexModels_DefaultOptions_SameContent()
        {
            const int id = 123;
            const string value = "fnord";
            var date = DateTime.Now;

            const string link = @"http://example.com/subpage/?param=1&other=test";

            var options = new CompareOptions().Use(EqualityComparer<Uri>.Default);

            var complex1 = new ComplexModel
            {
                Codes = new HashSet<string>(new[] { "A", "b", "cee" }),
                Link = new Uri(link),
                NumberSequence = new List<int> { 1, 2, 3 },
                Simple = new SimpleModel
                {
                    Id = id,
                    Timestamp = date,
                    Value = value
                },
                Simples = new[]
                {
                    new SimpleModel
                    {
                        Id = id+1,
                        Timestamp = date,
                        Value = value
                    },
                    new SimpleModel
                    {
                        Id = id+2,
                        Timestamp = date,
                        Value = value
                    }
                },
                Nested = new ComplexModel
                {
                    Codes = new HashSet<string>(new[] { "test1", "test2" })
                }
            };

            var complex2 = new ComplexModel
            {
                Codes = new HashSet<string>(new[] { "A", "b", "cee" }),
                Link = new Uri(link),
                NumberSequence = new List<int> { 1, 2, 3 },
                Simple = new SimpleModel
                {
                    Id = id,
                    Timestamp = date,
                    Value = value
                },
                Simples = new[]
                {
                    new SimpleModel
                    {
                        Id = id+1,
                        Timestamp = date,
                        Value = value
                    },
                    new SimpleModel
                    {
                        Id = id+2,
                        Timestamp = date,
                        Value = value
                    }
                },
                Nested = new ComplexModel
                {
                    Codes = new HashSet<string>(new[] { "test1", "test2" })
                }
            };
            ReferenceEquals(complex1, complex2).Should().BeFalse("They are not the same objects");
            ObjectComparer.Compare(complex1, complex2, out _, options).Should().BeTrue("they contain the same data");
        }

        [Fact]
        public void Compare_ComparesComplexModels_DefaultOptions_DifferentContent()
        {
            const int id = 123;
            const string value = "fnord";
            var date = DateTime.Now;

            const string link = @"http://example.com/subpage/?param=1&other=test";

            var options = new CompareOptions().Use(EqualityComparer<Uri>.Default);

            var complex1 = new ComplexModel
            {
                Codes = new HashSet<string>(new[] { "A", "b", "cee", "this should make them different" }),
                Link = new Uri(link),
                NumberSequence = new List<int> { 1, 2, 3 },
                Simple = new SimpleModel
                {
                    Id = id,
                    Timestamp = date,
                    Value = value
                },
                Simples = new[]
                {
                    new SimpleModel
                    {
                        Id = id+1,
                        Timestamp = date,
                        Value = value
                    },
                    new SimpleModel
                    {
                        Id = id+2,
                        Timestamp = date,
                        Value = value
                    }
                },
                Nested = new ComplexModel
                {
                    Codes = new HashSet<string>(new[] { "test1", "test2" })
                }
            };

            var complex2 = new ComplexModel
            {
                Codes = new HashSet<string>(new[] { "A", "b", "cee" }),
                Link = new Uri(link),
                NumberSequence = new List<int> { 1, 2, 3 },
                Simple = new SimpleModel
                {
                    Id = id,
                    Timestamp = date,
                    Value = value
                },
                Simples = new[]
                {
                    new SimpleModel
                    {
                        Id = id+1,
                        Timestamp = date,
                        Value = value
                    },
                    new SimpleModel
                    {
                        Id = id+2,
                        Timestamp = date,
                        Value = value
                    }
                },
                Nested = new ComplexModel
                {
                    Codes = new HashSet<string>(new[] { "test1", "test2" })
                }
            };
            ReferenceEquals(complex1, complex2).Should().BeFalse("They are not the same objects");
            ObjectComparer.Compare(complex1, complex2, out _, options).Should().BeFalse("they do not contain the same data");
        }
    }
}
