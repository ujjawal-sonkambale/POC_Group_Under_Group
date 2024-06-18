using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace QuestionTestingAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionnaireResponseController : ControllerBase
    {
        [HttpPost]
        [Route("process")]
        public IActionResult ProcessInput([FromBody] string input)
        {
            var result = QuestionProcessor.GetGroupItems(input);
            return Ok(result);
        }

        private class GroupItem
        {
            public string Id { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<GroupItem> SubGroups { get; set; }

            [JsonIgnore]
            public string RemainingComponents { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string LinkId { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string Question { get; set; }

            //[JsonIgnore]


            [JsonIgnore]
            public string ParentId { get; set; }

            [JsonIgnore]
            public string HierachyId { get; set; }

            [JsonIgnore]
            public int Generation { get; set; }

            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            [JsonPropertyName("items")]
            public List<GroupItem> QuestionList { get; set; }
        }

        private class QuestionItem
        {
            public string LinkId { get; set; }
            public string Question { get; set; }
            public List<QuestionItem> Items { get; set; }
        }

        private static class QuestionProcessor
        {
            static List<GroupItem> missingParentItems = new List<GroupItem>();

            public static List<GroupItem> GetGroupItems(string input)
            {
                missingParentItems.Clear();
                var output = GetParentGroupItems(input);
                var questionHierarchy = BuildQuestionHierarchy(output);
                var groups = BuildQuestionGroups(questionHierarchy);
                var finalGroups = BuildParentGroups(groups);
                return finalGroups;
            }

            private static List<GroupItem> GetParentGroupItems(string input)
            {
                var groupList = new List<GroupItem>();
                var groupTexts = input.Split(',');
                foreach (var groupText in groupTexts)
                {
                    groupList.Add(GetGroupItem(groupText));
                }
                return groupList;
            }

            private static GroupItem GetGroupItem(string groupText)
            {
                var groupItem = new GroupItem();
                var groupTextComponents = groupText.Split('^');
                var questionTotalText = groupText.Substring(groupText.LastIndexOf("/"));
                var questionComponents = questionTotalText.Split('^');
                groupItem.Id = questionComponents[0].Replace("/", "");
                groupItem.Question = groupTextComponents[1];
                groupItem.LinkId = groupText.Substring(0, groupText.IndexOf("^"));
                groupItem.Generation = groupTextComponents[0].Count(c => c == '/');
                var parentStringGroups = groupTextComponents[0].Split('/');
                groupItem.ParentId = parentStringGroups[parentStringGroups.Length - 2];
                groupItem.QuestionList = new List<GroupItem>();
                return groupItem;
            }

            private static List<GroupItem> BuildQuestionHierarchy(List<GroupItem> childList)
            {
                var groupList = new List<GroupItem>();
                var maxGeneration = childList.Max(c => c.Generation);
                var minGeneration = childList.Min(c => c.Generation);
                for (var i = maxGeneration; i >= minGeneration; i--)
                {
                    var childItems = childList.Where(ci => ci.Generation == i);
                    foreach (var item in childItems)
                    {
                        var parentItems = childList.Where(ci => ci.Generation == i - 1);
                        var parentItem = parentItems.Where(p => p.Id == item.ParentId).FirstOrDefault();
                        if (parentItem != null && !parentItem.QuestionList.Any(q => q.Id == item.Id))
                        {
                            parentItem.QuestionList.Add(item);
                        }
                        else
                        {
                            groupList.Add(item);
                        }
                    }
                }
                return groupList;
            }

            private static List<GroupItem> BuildQuestionGroups(List<GroupItem> childList)
            {
                var groupList = new List<GroupItem>();
                var maxGeneration = childList?.Max(c => c.Generation);
                for (var i = maxGeneration; i >= 0; i--)
                {
                    var childQuestionItems = childList.Where(ci => ci.Generation == i);
                    foreach (var item in childQuestionItems)
                    {
                        var groupItem = groupList.FirstOrDefault(g => g.Id == item.ParentId);
                        if (groupItem == null)
                        {
                            groupItem = new GroupItem() { QuestionList = new List<GroupItem>() };
                            groupItem.Id = item.ParentId;
                            groupItem.HierachyId = item.LinkId;
                            var linkComponents = item.LinkId.Split('/');
                            if (linkComponents.Length >= 3)
                            {
                                groupItem.ParentId = linkComponents[linkComponents.Length - 3];
                            }
                            groupItem.Generation = item.Generation - 1;
                            groupList.Add(groupItem);
                        }
                        groupItem.QuestionList.Add(item);
                    }
                }
                return groupList;
            }

            private static List<GroupItem> BuildParentGroups(List<GroupItem> childList)
            {
                var groupList = new List<GroupItem>();
                var generationHandle = new List<GroupItem>();
                groupList.AddRange(childList.Where(ci => ci.Generation == 1));
                var maxGeneration = childList?.Max(c => c.Generation);

                for (var i = maxGeneration; i > 1; i--)
                {
                    var childItems = childList.Where(ci => ci.Generation == i);
                    foreach (var childItem in childItems)
                    {
                        var testParent = CreateMissingParent(childItem);
                        var matchingParent = groupList.Where(gli => gli.Id == testParent.Id).FirstOrDefault();
                        if (matchingParent is null)
                        {
                            groupList.Add(testParent);
                        }
                        else
                        {
                            if (matchingParent.SubGroups is null)
                            {
                                matchingParent.SubGroups = new List<GroupItem>();
                            }

                            var newSubGroups = testParent.SubGroups.Where(gli => !matchingParent.SubGroups.Select(p => p.Id).Contains(gli.Id)).ToList();
                            matchingParent.SubGroups.AddRange(newSubGroups);
                        }
                    }
                }

                return groupList;
            }

            private static GroupItem CreateMissingParent(GroupItem groupItem)
            {
                GroupItem tempParent = null;

                for (int i = groupItem.Generation; i > 1; i--)
                {
                    var parentId = tempParent is null ? groupItem.ParentId : tempParent.ParentId;

                    var missingParent = missingParentItems.Where(mi => mi.Id == parentId).FirstOrDefault();
                    GroupItem workingItem = tempParent ?? groupItem;
                    if (missingParent is null)
                    {
                        var parentGroupItem = new GroupItem();
                        parentGroupItem.SubGroups = new List<GroupItem>();
                        parentGroupItem.Generation = i - 1;
                        parentGroupItem.HierachyId = workingItem.HierachyId;
                        parentGroupItem.Id = workingItem.ParentId;
                        parentGroupItem.ParentId = parentGroupItem.HierachyId.Split('/')[parentGroupItem.Generation - 1];
                        parentGroupItem.SubGroups.Add(workingItem);
                        tempParent = parentGroupItem;
                        missingParentItems.Add(parentGroupItem);
                    }
                    else
                    {
                        tempParent = missingParent;
                        tempParent.SubGroups.Add(workingItem);
                    }
                }
                return tempParent;
            }
        }
    }
}
