﻿using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using AdaSDK;
using System.Net.Http;
using System.Configuration;
using AdaSDK.Models;
using AdaBot.BotFrameworkHelpers; 
using System.Threading;
using AdaBot.Answers;
using AdaBot.Bot.Utils;

namespace AdaBot.Dialogs
{
    [Serializable]
    public class AdaDialog : LuisDialog<object>
    {
        public AdaDialog(params ILuisService[] services) : base(services)
        {

        }

        protected override async Task MessageReceived(IDialogContext context, IAwaitable<IMessageActivity> item)
        {
            AdaClient client = new AdaClient();
            var idUser = context.Activity.From.Id;

            var accessAllow = await client.GetAuthorizationFacebook(idUser);
            if (accessAllow == "false")
            {
                await context.Forward(new NotAllowedAdaDialog(
                new LuisService(new LuisModelAttribute(
                ConfigurationManager.AppSettings["ModelId"],
                ConfigurationManager.AppSettings["SubscriptionKey"]))),
                BasicCallback, context.Activity as Activity, CancellationToken.None);
            }
            else
            {
                var message = (Activity)await item;
                await base.MessageReceived(context, item);
            }
        }

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            await context.Forward(new TrivialLuisDialog(new LuisService(new LuisModelAttribute(
                           ConfigurationManager.AppSettings["ModelIdTrivial"],
                           ConfigurationManager.AppSettings["SubscriptionKeyTrivial"]))),
                   BasicCallback, context.Activity as Activity, CancellationToken.None);
        }

        [LuisIntent("What'sUp")]
        public async Task Event(IDialogContext context, LuisResult result)
        {
            CreateDialog createCarousel = new CreateDialog();

            Activity replyToConversation = await createCarousel.GetEvent(context);

            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        [LuisIntent("GetHelp")]
        public async Task GetHelp(IDialogContext context, LuisResult result)
        {
            CreateDialog createCarousel = new CreateDialog();

            Activity replyToConversation = createCarousel.GetHelp(context);


            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        private async Task BasicCallback(IDialogContext context, IAwaitable<object> result)
        {
            context.Wait(this.MessageReceived);
        }

        [LuisIntent("Possibilities")]
        public async Task Possibilities(IDialogContext context, LuisResult result)
        {
            CreateDialog createCarousel = new CreateDialog();

            Activity replyToConversation = createCarousel.CarouselPossibilities(context);

           
            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        [LuisIntent("BestFriend")]
        public async Task BestFriend(IDialogContext context, LuisResult result)
        {
            AdaClient client = new AdaClient();
            VisitDto bestFriend = await client.GetBestFriend();
            Activity replyToConversation;
            if (bestFriend == null)
            {
                replyToConversation = ((Activity)context.Activity).CreateReply($"Ca fait un moment que plus personne n'est venu me voir :'(");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message";
            }
            else
            {
                replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.Best.Spintax()} " + bestFriend.PersonVisit.FirstName + "!");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message";
                replyToConversation.AttachmentLayout = "carousel";
                replyToConversation.Attachments = new List<Attachment>();

                List<CardImage> cardImages = new List<CardImage>();
                cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(bestFriend.ProfilePicture.Last().Uri)}"));

                HeroCard plCard = new HeroCard()
                {
                    Title = bestFriend.PersonVisit.FirstName,
                    Images = cardImages
                };

                Attachment plAttachment = plCard.ToAttachment();
                replyToConversation.Attachments.Add(plAttachment);
            }
            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        [LuisIntent("GetVisitsToday")]
        public async Task GetVisitsToday(IDialogContext context, LuisResult result)
        {
            //Message d'attente
            await context.PostAsync($"{Dialog.Waiting.Spintax()}");

            AdaClient client = new AdaClient();
            List<VisitDto> visits = await client.GetVisitsToday();

            Activity replyToConversation;

            if (visits.Count == 0)
            {
                replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.Nobody.Spintax()}");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message";
            }
            else
            {
                replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.See.Spintax()} " + visits.Count + $" personnes aujourd'hui! :D");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message"; 
                replyToConversation.AttachmentLayout = "carousel";
                replyToConversation.Attachments = new List<Attachment>();

                int compteurCarrousel = 1;
                foreach (var visit in visits)
                {
                    if (compteurCarrousel <= 9 && result.Query != "GetVisitsTodayMoreResult")
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}"));

                        //Calcul la bonne année et la bonne heure.
                        DateTime today = DateTime.Today;
                        int yearVisit = visit.Date.Year;
                        int wrongDate = visit.PersonVisit.DateVisit.Year;
                        int goodDate = DateTime.Today.Year - wrongDate;
                        string messageDate = "";
                        string firstname;
                        DateTime visitDate = visit.PersonVisit.DateVisit;

                        //Recherche du prénom de la personne
                        if (visit.PersonVisit.FirstName == null)
                        {
                            firstname = "une personne inconnue";
                        }
                        else
                        {
                            firstname = visit.PersonVisit.FirstName;
                        }

                        var customDialog = new CreateDialog();
                        messageDate = customDialog.GetVisitsMessage(firstname, visitDate.AddYears(goodDate));

                        HeroCard plCard = new HeroCard()
                        {
                            Title = firstname,
                            Text = messageDate + " (" + Convert.ToString(visit.PersonVisit.DateVisit.AddHours(1).AddYears(goodDate)) + ")",
                            Images = cardImages
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteurCarrousel += 1;
                    }
                    else if (compteurCarrousel == 10 && result.Query != "GetVisitsTodayMoreResult")
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        CardImage img = new CardImage(url: $"{ConfigurationManager.AppSettings["IMGMore"]}");
                        cardImages.Add(img);

                        List<CardAction> cardButtons = new List<CardAction>();

                        CardAction plButtonChoice = new CardAction()
                        {

                            Value = "GetVisitsTodayMoreResult",
                            Type = "postBack",
                            Title = "J'en veux plus"
                        };
                        cardButtons.Add(plButtonChoice);


                        HeroCard plCard = new HeroCard()
                        {
                            Images = cardImages,
                            Buttons = cardButtons
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteurCarrousel += 1;
                    }
                    else if(result.Query == "GetVisitsTodayMoreResult")
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}"));

                        //Calcul la bonne année et la bonne heure.
                        DateTime today = DateTime.Today;
                        int yearVisit = visit.Date.Year;
                        int wrongDate = visit.PersonVisit.DateVisit.Year;
                        int goodDate = DateTime.Today.Year - wrongDate;
                        string messageDate = "";
                        string firstname;
                        DateTime visitDate = visit.PersonVisit.DateVisit;

                        //Recherche du prénom de la personne
                        if (visit.PersonVisit.FirstName == null)
                        {
                            firstname = "une personne inconnue";
                        }
                        else
                        {
                            firstname = visit.PersonVisit.FirstName;
                        }

                        var customDialog = new CreateDialog();
                        messageDate = customDialog.GetVisitsMessage(firstname, visitDate.AddYears(goodDate));

                        HeroCard plCard = new HeroCard()
                        {
                            Title = firstname,
                            Text = messageDate + " (" + Convert.ToString(visit.PersonVisit.DateVisit.AddHours(1).AddYears(goodDate)) + ")",
                            Images = cardImages
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteurCarrousel += 1;
                    }
                }
            }

            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        [LuisIntent("GetLastVisitPerson")]
        public async Task GetLastVisitPerson(IDialogContext context, LuisResult result)
        {
            string firstname = result.Entities[0].Entity;
            AdaClient client = new AdaClient();
            List<VisitDto> visits = await client.GetLastVisitPerson(firstname);

            Activity replyToConversation;

            if (visits.Count == 0)
            {
                replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.Unknow.Spintax()} " + firstname + $" :/ {Dialog.Presentation.Spintax()}");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message";
            }
            else
            {
                replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.IntroStats.Spintax()}");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message";
                replyToConversation.AttachmentLayout = "carousel";
                replyToConversation.Attachments = new List<Attachment>();

                int compteurCarrousel = 1;
                foreach (var visit in visits)
                {
                    if (compteurCarrousel <= 9)
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}")); // a mettre dans le SDK

                        //Calcul la bonne année et la bonne heure.
                        DateTime today = DateTime.Today;
                        int yearVisit = visit.Date.Year;
                        int wrongDate = visit.PersonVisit.DateVisit.Year;
                        int goodDate = DateTime.Today.Year - wrongDate;
                        string messageDate = "";
                        DateTime visitDate = visit.PersonVisit.DateVisit;

                        var customDialog = new CreateDialog();
                        messageDate = customDialog.GetVisitsMessage(firstname, visitDate.AddYears(goodDate));

                        HeroCard plCard = new HeroCard()
                        {
                            Title = visit.PersonVisit.FirstName,
                            Text = messageDate + " (" + Convert.ToString(visit.PersonVisit.DateVisit.AddHours(1).AddYears(goodDate)) + ")",
                            Images = cardImages
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteurCarrousel++;
                    }
                    else if (compteurCarrousel == 10)
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        cardImages.Add(new CardImage(""));

                        HeroCard plCard = new HeroCard()
                        {
                            Title = "Afficher plus (A venir)",
                            Text = "",
                            //Subtitle = 
                            Images = cardImages
                            //Buttons = cardButtons
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteurCarrousel += 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        [LuisIntent("GetStatsVisits")]
        public async Task GetLastVisGetStatsVisitsitPerson(IDialogContext context, LuisResult result)
        {
            //Message d'attente
            await context.PostAsync($"{Dialog.Waiting.Spintax()}");

            AdaClient client = new AdaClient();
            Activity replyToConversation;
            CreateDialog customDialog = new CreateDialog();
            TreatmentDialog treatment = new TreatmentDialog();
            EntityRecognizer recog = new EntityRecognizer();

            List<VisitDto> visitsReturn = new List<VisitDto>();
            List<VisitDto> tmp = new List<VisitDto>();
            List<ProfilePictureDto> EmotionPicture = new List<ProfilePictureDto>();
            bool askingEmotion = false;

            int nbEntities;
            int nbVisits = 0;
            string genderReturn = "personne(s)";
            string ageReturn = "";
            string emotionReturn = "";
            string emotion = "";
            string dateReturn = "aujourd'hui";
            string glassesReturn = "";
            string beardReturn = "";
            string mustacheReturn = "";

            DateTime? date1 = null;
            DateTime? date2 = null;
            int? age1 = null;
            int? age2 = null;
            GenderValues? gender = null;
            bool glasses = false;
            bool beard = false;
            bool mustache = false;

            var splitResult = result.Query.Split(':');

            if (splitResult[0] == "MoreGetStatsVisits")
            {
                if (splitResult[1] != "null")
                {
                   date1 = Convert.ToDateTime(splitResult[1]);

                    dateReturn = "le " + Convert.ToDateTime(date1).ToString("yyyy-MM-dd");

                    if (splitResult[2] != "null")
                    {
                        date2 = Convert.ToDateTime(splitResult[2]);
                        dateReturn = "entre le " + Convert.ToDateTime(date1).ToString("yyyy-MM-dd") + " et le " + Convert.ToDateTime(date2).ToString("yyyy-MM-dd");
                    }
                }
                if (splitResult[3] != "null")
                {
                    if (splitResult[3] == Convert.ToString(GenderValues.Female))
                    {
                        gender = GenderValues.Female;
                        genderReturn = "femme(s)";
                    } 
                    else if (splitResult[3] == Convert.ToString(GenderValues.Male))
                    {
                        gender = GenderValues.Male;
                        genderReturn = "homme(s)";
                    }
                }
                if (splitResult[4] != "null")
                {
                    age1 = Convert.ToInt32(splitResult[4]);
                    ageReturn = "de " + age1;

                    if (splitResult[5] != "null")
                    {
                        age2 = Convert.ToInt32(splitResult[5]);
                        ageReturn = "entre " + age1 + " et " + age2 + " ans";
                    }
                }
                if(splitResult[6] != "null")
                {
                    glasses = true;
                    glassesReturn = " avec des lunettes";
                }
                if (splitResult[7] != "null")
                {
                    beard = true;
                    beardReturn = "avec une barbe";
                }
                if (splitResult[8] != "null")
                {
                    mustache = true;
                    mustacheReturn = "et une moustache";
                }
            }
            else
            {
                //Composite Entities
                if (result.CompositeEntities != null)
                {
                    nbEntities = result.CompositeEntities.Count();
                    for (int i = 0; i < nbEntities; i++)
                    {
                        if (result.CompositeEntities[i].ParentType == "IntervalDate")
                        {
                            foreach (var entity in result.Entities)
                            {
                                if (entity.Type == "builtin.datetime.date")
                                {
                                    DateTime? date;
                                    List<EntityRecommendation> dates = new List<EntityRecommendation>();
                                    dates.Add(entity);
                                    recog.ParseDateTime(dates, out date);

                                    if (date1 == null)
                                    {
                                        date1 = date;
                                        dateReturn = "le " + Convert.ToDateTime(date1).ToString("yyyy-MM-dd");
                                    }
                                    else if (date1 != null)
                                    {
                                        date2 = date;
                                        if (date2 < date1 && date2 != null)
                                        {
                                            DateTime? tmpDate = date1;
                                            date1 = date2;
                                            date2 = tmpDate;
                                        }
                                        dateReturn = "entre le " + Convert.ToDateTime(date1).ToString("yyyy-MM-dd") + " et le " + Convert.ToDateTime(date2).ToString("yyyy-MM-dd");
                                    }
                                }
                            }
                        }
                        //Process of ages
                        if (result.CompositeEntities[i].ParentType == "SingleAge")
                        {
                            age1 = Convert.ToInt32(result.CompositeEntities[i].Children[0].Value);
                            ageReturn = "de " + age1;
                        }
                        else if (result.CompositeEntities[i].ParentType == "IntervalAge")
                        {
                            age1 = Convert.ToInt32(result.CompositeEntities[i].Children[0].Value);
                            age2 = Convert.ToInt32(result.CompositeEntities[i].Children[1].Value);
                            if (age2 < age1 && age2 != -1)
                            {
                                int? ageTmp = age1;
                                age1 = age2;
                                age2 = ageTmp;
                            }
                            ageReturn = "entre " + age1 + " et " + age2 + " ans";
                        }
                    }
                }

                //Single entities
                nbEntities = result.Entities.Count();
                for (int i = 0; i < nbEntities; i++)
                {
                    if (result.Entities[i].Type == "Gender")
                    {
                        string value = result.Entities[i].Entity;
                        gender = treatment.getVisitsByGender(value);

                        if (gender == GenderValues.Male)
                        {
                            genderReturn = "homme(s)";
                        }
                        else
                        {
                            genderReturn = "femme(s)";
                        }
                    }

                    if (result.Entities[i].Type == "Glasses")
                    {
                        glasses = true;
                        glassesReturn = " avec des " + result.Entities[i].Entity.ToString();
                    }
                    if (result.Entities[i].Type == "FacialHair")
                    {
                        string hair = result.Entities[i].Entity.ToString();
                        if (hair == "barbu" || hair == "barbus" || hair == "barbe")
                        {
                            beard = true;
                            beardReturn = "avec une barbe";
                        }
                        if (hair == "moustachu" || hair == "moustachus" || hair == "moustache")
                        {
                            mustache = true;
                            if (beardReturn != "")
                            {
                                mustacheReturn = "et une moustache";
                            }
                        }
                    }
                }
            }

            visitsReturn = await client.GetVisitsForStats(date1, date2, gender, age1, age2, glasses, beard, mustache);
            nbVisits = visitsReturn.Count();

            nbEntities = result.Entities.Count();
            for (int i = 0; i < nbEntities; i++)
            {
                if (result.Entities[i].Type == "Emotion")
                {
                    tmp = visitsReturn.ToList();
                    visitsReturn.Clear();
                    emotion = result.Entities[i].Entity;
                    //Pour le moment, on gère avec code (à modifier une fois Dico OK)
                    if (emotion == "heureux" || emotion == "heureuse" || emotion == "heureuses" || emotion == "souriant" || emotion == "souriants" || emotion == "souriante" || emotion == "souriantes")
                    {
                        emotion = "Happiness";
                        emotionReturn = "heureux(ses)";
                    }
                    if (emotion == "neutre" || emotion == "neutres")
                    {
                        emotion = "Neutral";
                        emotionReturn = "neutre(s)";
                    }
                    if (emotion == "triste" || emotion == "tristes")
                    {
                        emotion = "Sadness";
                        emotionReturn = "triste(s)";
                    }
                    if (emotion == "faché" || emotion == "fachés" || emotion == "fachée" || emotion == "fachées")
                    {
                        emotion = "Anger";
                        emotionReturn = "faché(es)";
                    }
                    if (emotion == "surpris" || emotion == "surprise" || emotion == "surprises")
                    {
                        emotion = "Surprise";
                        emotionReturn = "surpris(es)";
                    }

                    for (int y = 0; y < nbVisits; y++)
                    {
                        int nbEmotion = tmp[y].ProfilePicture.Count();
                        for (int z = 0; z < nbEmotion; z++)
                        {
                            if (customDialog.getEmotion(tmp[y].ProfilePicture[z].EmotionScore) == emotion &&
                            customDialog.getEmotion(tmp[y].ProfilePicture[z].EmotionScore) != null)
                            {
                                visitsReturn.Add(tmp[y]);
                                EmotionPicture.Add(tmp[y].ProfilePicture[z]);
                                askingEmotion = true;
                            }
                        }
                    }
                }
            }

            //NbPersonForReal
            nbVisits = visitsReturn.Count();
            int nbPerson = 0;
            nbPerson = treatment.getNbPerson(visitsReturn, nbPerson);

            //Return results
            if (nbPerson != 0)
            {
                replyToConversation = ((Activity)context.Activity).CreateReply("J'ai vu " + nbPerson + " " + genderReturn + " " + emotionReturn + " " + glassesReturn + " " + ageReturn + " " + beardReturn + " " + mustacheReturn + " " + dateReturn + ".");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message";
            }
            else
            {
                replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.NobodyStats.Spintax()} " + dateReturn + "... :/");
            }

            if (visitsReturn.Count() != 0)
            {
                replyToConversation.AttachmentLayout = "carousel";
                replyToConversation.Attachments = new List<Attachment>();
                int compteur = 0;

                int compteurCarrousel = 1;
                foreach (var visit in visitsReturn)
                {
                    if (compteurCarrousel <= 9 && splitResult[0] != "MoreGetStatsVisits")
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        if (!askingEmotion)
                        {
                            cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}")); // a mettre dans le SDK
                        }
                        else
                        {
                            cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(EmotionPicture[compteur].Uri)}")); // a mettre dans le SDK
                        }
                        string messageDate = "";
                        string firstname = "";

                        if (visit.PersonVisit.FirstName == null)
                        {
                            firstname = "une personne inconnue";
                        }
                        else
                        {
                            firstname = visit.PersonVisit.FirstName;
                        }
                        messageDate = customDialog.GetVisitsMessage(firstname, visit.Date);

                        HeroCard plCard = new HeroCard()
                        {
                            Title = firstname,
                            Text = messageDate + " (" + Convert.ToString(visit.Date.ToString("dd/MM/yyyy")) + ")",
                            Images = cardImages
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteur++;
                        compteurCarrousel++;
                    }
                    else if (compteurCarrousel == 10 && splitResult[0] != "MoreGetStatsVisits")
                    {
                        string buttonValue = treatment.GetValueButton(date1, date2, gender, age1, age2, glasses, beard, mustache);

                        List<CardImage> cardImages = new List<CardImage>();
                        CardImage img = new CardImage(url: $"{ConfigurationManager.AppSettings["IMGMore"]}");
                        cardImages.Add(img);

                        List<CardAction> cardButtons = new List<CardAction>();

                        CardAction plButtonChoice = new CardAction()
                        {

                            Value = "MoreGetStatsVisits:" + buttonValue,
                            Type = "postBack",
                            Title = "J'en veux plus"
                        };
                        cardButtons.Add(plButtonChoice);


                        HeroCard plCard = new HeroCard()
                        {
                            Images = cardImages,
                            Buttons = cardButtons
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteurCarrousel += 1;
                    }
                    else if (splitResult[0] == "MoreGetStatsVisits")
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        if (!askingEmotion)
                        {
                            cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}")); // a mettre dans le SDK
                        }
                        else
                        {
                            cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(EmotionPicture[compteur].Uri)}")); // a mettre dans le SDK
                        }
                        string messageDate = "";
                        string firstname = "";

                        if (visit.PersonVisit.FirstName == null)
                        {
                            firstname = "une personne inconnue";
                        }
                        else
                        {
                            firstname = visit.PersonVisit.FirstName;
                        }
                        messageDate = customDialog.GetVisitsMessage(firstname, visit.Date);

                        HeroCard plCard = new HeroCard()
                        {
                            Title = firstname,
                            Text = messageDate + " (" + Convert.ToString(visit.Date.ToString("dd/MM/yyyy")) + ")",
                            Images = cardImages
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                        compteur++;
                    }
                }
            }
            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        [LuisIntent("GetVisitsPersonByFirstname")]
        public async Task GetVisitsPersonByFirstname(IDialogContext context, LuisResult result)
        {
            await context.PostAsync($"{Dialog.Waiting.Spintax()}");

            Activity replyToConversation = null;
            AdaClient client = new AdaClient();

            int nbVisit = 10;

            var splitResult = result.Query.Split(':');

            if (splitResult[0] == "ChoosePersonId ")
            {

                int idPerson = Convert.ToInt32(splitResult[1]);

                nbVisit = Convert.ToInt32(splitResult[3]);

                List<VisitDto> visitsById = await client.GetVisitPersonById(idPerson, nbVisit);

                replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.VisitsPerson.Spintax()}");
                replyToConversation.Recipient = context.Activity.From;
                replyToConversation.Type = "message";
                replyToConversation.AttachmentLayout = "carousel";
                replyToConversation.Attachments = new List<Attachment>();

                foreach (var visit in visitsById)
                {
                    List<CardImage> cardImages = new List<CardImage>();
                    cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}"));

                    var customDialog = new CreateDialog();
                    var messageDate = customDialog.GetVisitsMessage(visit.PersonVisit.FirstName, visit.Date.AddHours(1));

                    HeroCard plCard = new HeroCard()
                    {
                        Title = visit.PersonVisit.FirstName,
                        Text = messageDate,
                        Images = cardImages
                    };

                    Attachment plAttachment = plCard.ToAttachment();
                    replyToConversation.Attachments.Add(plAttachment);
                }
            }
            else
            {
                string firstname = null;

                int nbEntities = result.Entities.Count();
                for (int i = 0; i < nbEntities; i++)
                {
                    if (result.Entities[i].Type == "Firstname")
                    {
                        firstname = result.Entities[i].Entity;
                    }
                    if (result.Entities[i].Type == "Number")
                    {
                        nbVisit = Convert.ToInt32(result.Entities[i].Entity);
                    }
                }

                List<VisitDto> visits = await client.GetLastVisitPerson(firstname);

                if (visits.Count == 0)
                {
                    replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.Unknow.Spintax()} " + firstname + $" :/ {Dialog.Presentation.Spintax()}");
                    replyToConversation.Recipient = context.Activity.From;
                    replyToConversation.Type = "message";
                }
                else if (visits.Count == 1)
                {
                    int id = visits[0].PersonVisit.PersonId;

                    List<VisitDto> visitsById = await client.GetVisitPersonById(id, nbVisit);

                    replyToConversation = ((Activity)context.Activity).CreateReply($"{Dialog.VisitsPerson.Spintax()}");
                    replyToConversation.Recipient = context.Activity.From;
                    replyToConversation.Type = "message";
                    replyToConversation.AttachmentLayout = "carousel";
                    replyToConversation.Attachments = new List<Attachment>();


                    foreach (var visit in visitsById)
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}"));

                        var customDialog = new CreateDialog();
                        var messageDate = customDialog.GetVisitsMessage(visit.PersonVisit.FirstName, visit.Date.AddHours(1));

                        HeroCard plCard = new HeroCard()
                        {
                            Title = visit.PersonVisit.FirstName,
                            Text = messageDate,
                            Images = cardImages
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                    }

                }
                else
                {
                    replyToConversation = ((Activity)context.Activity).CreateReply("Je connais " + visits.Count + " " + firstname + ". Les voici :)");
                    replyToConversation.Recipient = context.Activity.From;
                    replyToConversation.Type = "message";
                    replyToConversation.AttachmentLayout = "carousel";
                    replyToConversation.Attachments = new List<Attachment>();

                    foreach (var visit in visits)
                    {
                        List<CardImage> cardImages = new List<CardImage>();
                        cardImages.Add(new CardImage(url: $"{ ConfigurationManager.AppSettings["WebAppUrl"] }{VirtualPathUtility.ToAbsolute(visit.ProfilePicture.Last().Uri)}")); // a mettre dans le SDK

                        List<CardAction> cardButtons = new List<CardAction>();

                        CardAction plButtonChoice = new CardAction()
                        {

                            Value = "ChoosePersonId :" + visit.PersonVisit.PersonId + " : nbVisit :" + nbVisit,
                            Type = "postBack",
                            Title = "Le voilà !"
                        };
                        cardButtons.Add(plButtonChoice);

                        HeroCard plCard = new HeroCard()
                        {
                            Title = visit.PersonVisit.FirstName,
                            Images = cardImages,
                            Buttons = cardButtons
                        };

                        Attachment plAttachment = plCard.ToAttachment();
                        replyToConversation.Attachments.Add(plAttachment);
                    }
                }
            }

            await context.PostAsync(replyToConversation);
            context.Wait(MessageReceived);
        }

        [LuisIntent("GetAverageVisits")]
        public async Task GetAverageVisits(IDialogContext context, LuisResult result)
        {
            AdaClient client = new AdaClient();

            GenderValues? gender;
            int? age1 = null;
            string ageReturn1 = "null";
            int? age2 = null;
            string ageReturn2 = "null";
            string ageReturn = "";
            string genderReturn = "null";

            int nbEntities = result.Entities.Count();
            for (int i = 0; i < nbEntities; i++)
            {
                if (result.Entities[i].Type == "Gender")
                {
                    string value = result.Entities[i].Entity;

                    gender = GenderValues.Male;
                    if (value == "femme" || value == "femmes" || value == "fille" || value == "filles")
                    {
                        gender = GenderValues.Female;
                    }
                    genderReturn = Convert.ToString(gender.Value);

                }
            }

            if (result.CompositeEntities != null)
            {
                nbEntities = result.CompositeEntities.Count();
                for (int i = 0; i < nbEntities; i++)
                {
                    //Process of ages
                    if (result.CompositeEntities[i].ParentType == "SingleAge")
                    {
                        age1 = Convert.ToInt32(result.CompositeEntities[i].Children[0].Value);
                        ageReturn1 = Convert.ToString(age1);
                        ageReturn = " de " + age1 + " ans";
                    }
                    else if (result.CompositeEntities[i].ParentType == "IntervalAge")
                    {
                        age1 = Convert.ToInt32(result.CompositeEntities[i].Children[0].Value);
                        age2 = Convert.ToInt32(result.CompositeEntities[i].Children[1].Value);
                        if (age2 < age1 && age2 != -1)
                        {
                            int? ageTmp = age1;
                            age1 = age2;
                            age2 = ageTmp;
                        }
                        ageReturn1 = Convert.ToString(age1);
                        ageReturn2 = Convert.ToString(age2);

                        ageReturn = " entre " + age1 + " et " + age2 + " ans";
                    }
                }

            }

            int nbVisits = await client.GetNbVisits(genderReturn, ageReturn1, ageReturn2);

            if (genderReturn == "null")
            {
                genderReturn = "personne(s)";
            }
            else if (genderReturn == Convert.ToString(GenderValues.Female))
            {
                genderReturn = "femme(s)";
            }
            else
            {
                genderReturn = "homme(s)";
            }

            string message = "Je vois en moyenne : " + nbVisits + " " + genderReturn + " " + ageReturn + " par jour.";
            await context.PostAsync(message);
            context.Wait(MessageReceived);
        }
    }
}